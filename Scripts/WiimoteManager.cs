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
			
			_dataStream = null;
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
						//once this stream is established, STOP.
						break;
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
			_dataStream.Write(modes, 0, modes.Length);
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
		//This basically treats the wiimote as a mouse. Pointing logic is quite simple.
		//Touchmote is the software I landed upon.
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
