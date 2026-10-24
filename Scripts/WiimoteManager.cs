using Godot;
using System;
using System.Threading.Tasks;
using HidSharp;

public partial class WiimoteManager : Node2D
{
	
	//this is the nintendo Bluetooth ID. 
	// The computer in use will search for a device giving off this ID.
	private const int NintendoVendorId = 0x057e;
	
	//0x0306 is the Wiimote product id.
	//0x0330 is the wiimote+ product id.
	private const int WiimoteProductIdStandard = 0x0306;
    private const int WiimoteProductIdPlus = 0x0330;



	//Nintendo has four operating states that we can pull from. They are as follows:
	//0x30 = Core buttons Only
	//0x31 = Core Buttons + accelerometer
	//0x33 = Core buttons + accelerometer + Basic Infrared reading.
	//0x37 = More precise 0x33 mode. 'full-power'.

	//Additionally, there are bytes to be sent that have functionality.
	//0x12 tells the controller to switch modes.
	//0x04, which we will use, tells the controller to always scan for input.
	//0x00 is an alternative to that, which would wait till the player pushes a button.
	private const byte ChangeOperatingMode = 0x12;
	private const byte ContinousReporting = 0x04;
	private const byte IRWiimoteMode = 0x33;

	//these bytes are used for writing to register.
	private const byte WriteMemoryReport = 0x16;
	private const byte MemoryAddressPart1 = 0xb0;
	private const byte MemoryAddressPart2 = 0x00;
	private const byte MemoryAddressPart3 = 0x30;
	private const byte PayloadDataSize = 0x01;
	private const byte CameraEnableValue = 0x08;
	private const byte ControlRegisterFlag = 0x04;
	//Bitwise masking will be used here to manage network and bluetooth bandwidth.
	//example
	//00001010  (Incoming buffer[2] data: A is pressed, B is not, something else is)
	//& 00001000  (Our ButtonAMask: 0x08)
	//----------
  	//00001000  (Result is not zero. The A button is pressed)

	private const byte ButtonAMask = 0x08;
	private const byte ButtonBMask = 0x04;

	private const int InputBufferSize = 22;

	//volatile so that the data isn't cached. All these volatile methods are for Godot to play nice with threads.
	private volatile bool _isReading;
	private volatile bool _isAPressed;
	private volatile bool _isBPressed;

	//past tense versions of the button pressing to allow for coding that prevents like 1000 things menus from opening on a single click.
	private bool _wasAPressed;
	private bool _wasBPressed;
	
	//These variables make it clear what hardware to call.
	private HidDevice _connectedDevice;
	private HidStream _dataStream;

	public Vector2 CurrentAimPosition { get; private set; }
	//Variables to store 'A' and 'B' so it's easier to call later.
	private const string WiiAAction = "wii_a_button";
    private const string WiiBAction = "wii_b_trigger";

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		//Task.run should set the ConnectToWiimote method to be a separate thread.
		//Without it, Godot would literally just wait until windows was 'done' scanning for bluetooth.
		OnWiimoteAClicked += HandleWiimoteA;
		OnWiimoteBClicked += HandleWiimoteB;
		
		Task.Run(() =>
		{
			ConnectToWiimote();
		});
	}

    public override void _ExitTree()
    {
        if( _dataStream != null)
		{
			try
			{
				_isReading = false;
				_dataStream.Dispose();
			}
			catch (Exception ex)
			{

				GD.PushWarning($"Non-critical error disposing Wiimote stream: {ex.Message}");
			}
			finally
			{
				_dataStream = null;
			}
		}
    }


	private void HandleWiimoteA()
	{
		GD.Print("Wiimote 'A button' clicked!");
	}
	private void HandleWiimoteB()
	{
		GD.Print("Wiimote 'B button' clicked!");
	}
	//This method is intended to read the data stream from the wiimote and check for button presses and interact with IR data.
	private void ReadIncomingData()
	{
		byte[] buffer = new byte[InputBufferSize];
		//These variables are used to store the IR dot positions.
		//The wiimote calculates the center of the screen based on Dot 1 + Dot 2 / 2. 
		int dot1X;
		int dot1Y;
		int dot2X;
		int dot2Y;
		while(_isReading)
		{
			try
			{
				_dataStream.Read(buffer, 0 , buffer.Length);
				//We look for the A button byte mask to match the buffer recieved.
				//if the two bytes do not match, the & comparator returns false and A is not pressed.
				_isAPressed = ((buffer[2] & ButtonAMask) != 0);
				if(_isAPressed && !(_wasAPressed))
				{
					Callable.From(() => OnWiimoteAClicked?.Invoke()).CallDeferred();
				}
				//We look for the B button byte mask to match the buffer recieved.
				//if the two bytes do not match, the & comparator returns false and B is not pressed.
				_isBPressed = ((buffer[2] & ButtonBMask) != 0);
				if(_isBPressed && !(_wasBPressed))
				{
					Callable.From(() => OnWiimoteBClicked?.Invoke()).CallDeferred();
				}

				_wasAPressed = _isAPressed;
				_wasBPressed = _isBPressed;
				//Here we are building a 10 bit integer coorindate with the 8 bit pieces nintendo provides. 
				//It is a bit confusing at first, ask me(Ford) if confused. Easier than it looks!
				
				//In essence. Nintendo provides 10 bits of data for the X and Y coordinates, but the last two bits are stored in a different byte.
				//This is why we have to bitwise mask the 8 bit data and then shift it over to the left by 4 or 2 bits to make room for the last two bits. (x coorindate uses bits 4 and 5, y coorindate uses bits 6 and 7 of byte[8]).
				//notice that both the x and the y coordinate both share the same byte for their last two bits. This is why we have to bitwise mask the data to get the correct bits for each coordinate.
				//That also optimizes space, as we use the same byte for both coordinates.


				if (buffer[6] != 0xFF)
				{
					//this line of code is clicking both bytes together to make a 10 bit integer for the x coordinate. The first part is the 8 bit data, the second part is the last two bits of data that are stored in a different byte.
    				dot1X = buffer[6] | ((buffer[8] & 0x30) << 4);
					//this line of code is clicking both bytes together to make a 10 bit integer for the y coordinate. The first part is the 8 bit data, the second part is the last two bits of data that are stored in a different byte.
    				dot1Y = buffer[7] | ((buffer[8] & 0xC0) << 2);
    				GD.Print($"Wiimote IR Dot 1 Position: ({dot1X}, {dot1Y})");
				}
				//Same thing. This is just the y coordinate following the same logic with the next avaliable bytes.
				if(buffer[9] != 0xFF)
				{
					dot2X = buffer[9] | ((buffer[11] & 0x30) << 4); //should add variables here to make what these hex values do more clear. They're just bit masks.
					dot2Y = buffer[10] | ((buffer[11] & 0xC0) << 2);
				}



			}
			catch (Exception ex)
			{
				_isReading = false;
				GD.PushError($"Data not reading from Wii Remote properly: {ex.Message}");
			}
		}
	}






	//This method is intended to allow windows blueooth to work with the game.
	private void ConnectToWiimote()
	{
		try
		{	
			//HidSharp library that has been imported will search for the variable we established earlier.
			//That variable simply has the NintendoVendorID stored.
			foreach(HidDevice device in DeviceList.Local.GetHidDevices(NintendoVendorId))
			{
				if (device.ProductID == WiimoteProductIdStandard || device.ProductID == WiimoteProductIdPlus)
				{
					_connectedDevice = device;
					//this should open data pipe.
					if(_connectedDevice.TryOpen(out _dataStream))
					{
						GD.Print($"Successfully opened native HID stream: {_connectedDevice.DevicePath}");
						InitializeWiimote();
						_isReading = true;

						Task.Run(() => ReadIncomingData()); 
						//task.run once more makes a separate worker thread handle this infinite loop.
						
						break; //once this stream is established, STOP.
					}
				}
			}
			if (_dataStream ==null)
			{
				GD.Print("No controller found, using mouse.");
			}
		}
		catch (Exception ex)
		{
			GD.PushError($"Error initializing native Wiimote HID connection: {ex.Message}");
		}
		
	}

	private void InitializeWiimote()
	{
		try
		{
			byte[] modes = {ChangeOperatingMode , ContinousReporting , IRWiimoteMode };
			byte[] firstCameraRegister = {WriteMemoryReport, ControlRegisterFlag, MemoryAddressPart1, MemoryAddressPart2, MemoryAddressPart3, PayloadDataSize, CameraEnableValue};
			//second is very similar, but not identical.
			byte[] secondCameraRegister = {WriteMemoryReport, ControlRegisterFlag, MemoryAddressPart1, MemoryAddressPart2, MemoryAddressPart2, PayloadDataSize, CameraEnableValue};
			_dataStream.Write(modes, 0, modes.Length);
			_dataStream.Write(firstCameraRegister, 0, firstCameraRegister.Length);
			//should give the microcontroller time to store into the first register before trying to move on.
			System.Threading.Thread.Sleep(50);
			_dataStream.Write(secondCameraRegister, 0, secondCameraRegister.Length);

			
		}
		catch (Exception ex)
		{
			GD.PushError($"Error initializing Wiimote reporting mode: {ex.Message}");
		}
	}
	public event Action OnWiimoteAClicked;
	public event Action OnWiimoteBClicked;


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	//This is a Godot thing^.
	public override void _Process(double delta)
	{

		//This will check if the bluetooth for wii remote is working.
		//If the wii remote is not working, fall back to mouse controls.
		if(_dataStream ==null)
		{
			CurrentAimPosition = GetGlobalMousePosition();

		if(Input.IsActionJustPressed(WiiAAction))
		{
			OnWiimoteAClicked?.Invoke();

			GD.Print("Wiimote 'A button' clicked at: " + CurrentAimPosition);
		}

		if(Input.IsActionJustPressed(WiiBAction))
		{
			OnWiimoteBClicked?.Invoke();

			GD.Print("Wiimote 'B button' clicked at: " + CurrentAimPosition);
		}
		}
	}
}
