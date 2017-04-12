# Home Assistant Windows Tools
This project contains tools to make Home Assistant easier to use on windows.

## gatttool
This tool emulates the linux bluez stack gatttool, it currently only supports `--char-read` and `--char-write-req` functionality but that is enough to run `Mi Flora Plant Sensor`.
> Only tested on `Windows 10`, not compatible with `Windows 7` but could work on `Windows 8` and `Windows 8.1`.

## hcitool
This tool emulates the linux bluez stack hcitool, it currently only supports `lescan`.
> You will have to pair the `Bluetooth LE` device in Windows before this tool will be able to detect it.
> Only tested on `Windows 10`, not compatible with `Windows 7` but could work on `Windows 8` and `Windows 8.1`.

## HomeAssistantService
This tool allows you to run HASS as a `Windows service` also implements a check every 30 seconds to ensure that HASS is responsive.

### Installation
* Copy the `HomeAssistantService` folder to a location of choice for example `C:\Program files\HomeAssistantService\`
* Open cmd and go to the directory where you placed `HomeAssistantService`
* Run `HomeAssistantService install`, this installs it as a windows service.
* Provide the credentials for the current user when prompted (This is used to run the service as the current user)
* Run `HomeAssistantService start` this starts the service and in turn `Home Assistant`.

### Uninstall
* Open cmd and go to the directory where you placed `HomeAssistantService`
* First stop the service with `HomeAssistantService stop`
* Run `HomeAssistantService uninstall` to uninstall the service

## IISRPWA - IIS Reverse Proxy with authentication
This allows you to turn IIS into a Reverse proxy with multiple users.

> Passwords are stored using Salt and key stretching with SHA512.

### Installation
* Create a new site in IIS
* Add a HTTPS Cert
* Copy IISRPWA to the iis site directory
* Run App_Data\IISRPWA.Manager.exe
* add at least one user
* If your not using `Let's Encrypt` remove PathException `~/.well-known/acme-challenge/*`
* If you wish to be able to access HASS without authenticating at home add a ip exception specific for your ip or with `192.168.0.*` (replace `0` if your using a diffrent subnet)
* Open web.config change the ip address on the following line `<action type="Rewrite" url="http://192.168.0.15/{R:1}"/>`.

### Requirments
* URL Rewrite for IIS
* Websocket support for IIS

## IISRPWA.Manager
This tool allows you to manage the Configuration.config for IISRPWA.
