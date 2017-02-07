#Introduction 
TaBot is a pernonnel assistant using Microsoft bot framework, Microsoft Azure services and Microsoft Win 10 IoT core device. Please see Arch.docx for the architecture of the system.

##Repository contents
### [AlarmMonitor](https://github.com/xiangyan99/TaBot/tree/master/AlarmMonitor)
A standalone windows desktop application monitors Azure event hub in real time.

###[GpioOneWire](https://github.com/xiangyan99/TaBot/tree/master/IoT/GpioOneWire)
A WinRT component implemented in VC which is used to get temperature on IoT device.

###[TaBot](https://github.com/xiangyan99/TaBot/tree/master/TABot)
The bot code implemented with Microsoft Bot Framework.

###[TaApis](https://github.com/xiangyan99/TaBot/tree/master/TaApis)
The backend apis which implements the function of set alarm, forword command, query temperature, etc.

###[TaClient](https://github.com/xiangyan99/TaBot/tree/master/TaClient)
A UWP app which uses speech to text APIs to interpret voice into text and communicate with the bot.

###[tauwp](https://github.com/xiangyan99/TaBot/tree/master/tauwp)
A UWP app running on Win 10 IoT core to collect temperature from IoT device and send the data to IoT hub.
