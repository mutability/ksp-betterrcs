BetterRCS
0.1 - 2012/12/26

This module is a plugin for Kerbal Space Program version 0.18.2.
It provides a drop-in replacement for the default ModuleRCS module,
tweaking the RCS behaviour slightly:

(1) The requiresFuel config parameter is respected (if false, no fuel is
    required or consumed). Stock KSP ignores this parameter. This doesn't
    affect stock parts.
    
(2) RCS thrust responds over the whole range of control input regardless of
    lever arm length. Stock KSP will reach max thrust in response to small
    control inputs if the thruster has a long lever arm.

(3) Requested thrust for a rotational input uses the RCS thruster position,
    not the RCS part position. Stock KSP uses the RCS part position to
    compute thrust. Both modules use the RCS thruster position to compute the
    resulting torque. This doesn't affect stock parts, as they have thruster
    positions that are basically right on top of the part position anyway.

(4) When both rotational and translational inputs are present, and for a
    particular RCS part the requested forces are in opposing directions, the
    net force is used to command thrust. Stock KSP would generate thrust for
    both forces independently, producing cases where two opposite thrusters
    on the same RCS block would be firing simultaneously against each other.

(5) More stats in the right-click window: current thruster state, total
    thrust, generated torque, net force, and fuel flow rate. Also, reported
    Isp is updated even if the thruster is not in use.

To use it:

  * Copy Plugins\BetterRCS.dll to <KSP>\Plugins\
  * If you want to update the standard RCS parts to use BetterRCS, copy the
    subdirectories of Parts\ to <KSP>\Parts\, replacing the existing files.
    Only part.cfg is updated - the other files should be kept.
  * To use in other RCS parts, just change ModuleRCS to ModuleBetterRCS in
    part.cfg.

To compile it: 

  * Source code is available from github:
    https://github.com/mutability/ksp-betterrcs/
  * There are solution / project files included. You may need to tell it
    where your KSP assemblies are. I built with SharpDevelop - you may
    need to tweak things if using other tools.
  
License: 

  * Source & DLL are licensed under a BSD 2-clause license. See the header in
    the source code, or the file COPYING.txt.   
  * The provided part.cfg files are trivially modified versions of the stock
    KSP part files; I claim no copyright there.

Feedback to Oliver Jowett <oliver@mutability.co.uk>
