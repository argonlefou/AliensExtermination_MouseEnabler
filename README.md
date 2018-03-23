# Aliens Extermination - Mouse Enabler

This arcade game is nativelly working only with analog joypad ( Mounted Guns )  .  
This small tool will hook the game's process and allow to play it with a mouse. 
This is just a Proof Of Concept application, and absolutelly not meant to be a final / frontend friendly application.  

## How to use :

You can use the application by either running it from the file explorer or by command line. 
The game's window size should be detected automatically, however for some people Windows is reporting a NULL size window and the target is not moving from the top left corner.    
In that case, you can run it from a command line and add some arguments to force the resolution :
`Aliens_MouseEnabler.exe -resX=1920 -rezY=1080`

You can then run the game. You have to let this program running while you're playing

## Important

1) This tool will only work with the latest release, containing the unpacked/modified executable (called **aliens dehasped.exe**).

2) When you run the game for the first time, go to the TEST menu and run a gun calibration to have accurate aiming.

3) If you change the game's resolution (only available by patching the executable), you'll need to re-run the calibration.

## Controls

<table>
  <tr>
    <td colspan="4" align="center"><b>Aliens Extermination Controls</b>
  </tr>  
  <tr>
    <td><b>Mouse Buttons</b></td>
    <td align="center">Left</td>
    <td align="center">Middle</td>
    <td align="center">Right</td>
  </tr>  
<tr>
     <td><b>Action</b></td>
    <td align="center"><i>Shoot</i></td>
    <td align="center"><i>Grenade</i></td>
    <td align="center"><i>Flame Thrower</i></td>
  </tr>  
</table>