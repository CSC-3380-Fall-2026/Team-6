using Godot;
using System;

/* SUMMARY
Any script that needs to run within the fishing scene can go here.
*/
public partial class GoneFishing : Node
{
    // Fishing rod mechanics have been decoupled to FishingRod.cs.
    // Use this script for future scene-wide management (score, timers, pausing, etc.).

    public override void _Ready()
    {
        AudioManager.Instance?.PlayMusic(AudioManager.GoneFishingMusic);
    }
}