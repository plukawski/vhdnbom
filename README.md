## VHD Native Boot Os Migrator (VHDNBOM)

## Description
Are you tired of constantly reainstalling Windows and applications and then applying all your settings from scratch after you buy new laptop/pc or replace you current hardware during RMA? If yes, then this tool is for you.

**VHDNBOM** tool allows migration of currently installed Windows Operating system into `VHDX` image which then can be natively booted from boot manager. This allows your operating system to always reside within an image file (like it is done in Virtual Machines), which greatly simplifies future hardware changes to not require reinstallation of whole operating system and applications.
In the future in case of the hardware changes or failures instead of installing and configuring the system from scratch, you have only to place the `VHDX` file created by this tool on the drive and add it to the boot manager. After booting you will have your old, fully configured and customized system up and ready. Just ensure to make periodical backups of the image, to have the most recent system state always ready for you.

What exactly this tool does is the following:
1. Creates a **VHDX** image file containing a full copy of you current Windows system drive and places it on any other drive which has enough free space to hold it (the tool automatically detects the drive with the biggest amount of free space which is suitable as a temporary image storage).
1. Prepares the image to be native booted (by doing small registry fixes and removing not needed files like pagefile, hiberfil, etc.).
1. By default shrinks the image and moves it to the system drive.
1. Optionally adds the created image to the boot manager, so you can boot directly from it instead of physical disk partition. To do th8is you have to specify `-AddToBootManager` parameter.

**NOTE**: The image created by this tool is also suitable to be launched from Virtual Machine. However in such situation you have to create the boot partition with boot manager on a separate disk and properly add the partition from the VHDNBOM image to the boot menu.

## Reqirements

1. Windows 7 Enterprise/Ultimate or any version of Windows 8/8.1/10 operating systems. 
**NOTE**: Windows 7 Starter, Home, Home Premium, Professional are not supported.
1. Second partition/drive which has enough free space to temporarily store the image of the full system drive. So if the system drive has 256GB capacity, then at least 256GB of free space plus 200MB must be available on any other partition or drive.
1. Bitlocker turned off.
1. Local administrator rights.
1. At least 51% of free disk space on the system drive - if you want the image to reside on the system drive, which is the default behavior.


## Usage
VHDNBOM is a command line tool. Here are the sample usages.

- To migrate current os to image, store it on the system drive and add an entry to the boot manager:

```
vhdnbom.exe -mode MigrateCurrentOsToVhd -addToBootManager
```

- To migrate current os to image, store it on the other drive (it automatically detects the drives suitable as the image storage and picks the one with the biggest amount of the free space), shrink it to be able to fit in system drive and add an entry to the boot manager:

```
vhdnbom.exe -mode CreateCurrentOsVhdOnly -addToBootManager
```

- To migrate current os to image, store it in the specified location (here it will be `d:\VHD_Boot`) and add an entry to the boot manager:

```
vhdnbom.exe -mode CreateCurrentOsVhdOnly -addToBootManager -vhdTempFolderPath d:\VHD_Boot
```

There is also `-quiet` parameter you can add. When added there will be no interaction with the user - usefull when running this tool from within an external script or from the task scheduler.

**NOTE:** By default, when no parameters are specified the migrator will migrate the os to image, shrink it and store that image on system drive. However it will not add it to boot manager, so in order to boot from it you will have to manually add that image to boot manager using `bcdedit` command.

**NOTE2:** If you specified that the image should be added to boot menu, the entry in the menu will be named `VHDNBOM OS Image`.

**NOTE3**: After the migration was performed you should try booting into the image to ensure if everything is working correctly in the migrated system. If everything works fine, you can then remove the original system files from the physical partition the image has been created from and set the boot from the image as the default option using either `bcdedit` command or control panel.

## License

This tool is being licensed under MIT License.

## Building the project
To build the project you have to install Visual Studio 2017 Community or newer with full .NET desktop c# development support. 

Then just open the VHDNBOM.sln file and build the solution. After the build is completed go into `VHDNBOM\Bin\Debug` folder and launch `VHDNBOM.exe` using the command line.
