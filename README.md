# Overview
This program is meant to convert the executable files from Shadow of the Colossus (PS2) into a file that can be analyzed inside of ghidra. SotC uses a proprietary format for storing dynamically linked libraries (.XFF files) which cannot be loaded into Ghidra by default because function calls and object locations are relocated at runtime. This program reads the XFF files and main executable to find and apply these relocations and creates debug information for analysis in ghidra, including importing symbols.

To use run "./XFF (Input Directory) (Output Directory)

The input directory should be the .ISO files that have been extracted to a folder and contain the XFF's (GAMECORE.XFF, KERNEL.XFF, and MANAGER.XFF) and main executable (i.e. SCUS-*)
