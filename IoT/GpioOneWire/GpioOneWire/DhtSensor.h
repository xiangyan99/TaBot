#pragma once
#include <bitset>

namespace GpioOneWire
{
    public ref class DhtSensor sealed
    {
		enum { SAMPLE_HOLD_LOW_MILLIS = 18 };
    public:
		DhtSensor() :
			pin(nullptr),
			inputDriveMode(Windows::Devices::Gpio::GpioPinDriveMode::Input)
		{ }

		void Init(Windows::Devices::Gpio::GpioPin^ Pin);
		
		bool PullResistorRequired()
		{
			return inputDriveMode != Windows::Devices::Gpio::GpioPinDriveMode::InputPullUp;
		}

		bool IsValid()
		{
			unsigned long long value = this->bits.to_ullong();
			unsigned int checksum =
				((value >> 32) & 0xff) +
				((value >> 24) & 0xff) +
				((value >> 16) & 0xff) +
				((value >> 8) & 0xff);

			return (checksum & 0xff) == (value & 0xff);
		}

		double ReadTemperature()
		{
			int result = Sample();
			if (result == S_OK)
			{
				unsigned long long value = this->bits.to_ullong();
				unsigned long long temp1 = value >> 32;
				double temp = (temp1 & 0x7FFF) * 1.0;
				if ((value >> 8) & 0x8000)
					temp = -temp;
				return temp * 9 /5 + 32;
			}
			else
				return -1;
		}

	private:
		Windows::Devices::Gpio::GpioPin^ pin;
		Windows::Devices::Gpio::GpioPinDriveMode inputDriveMode;
		std::bitset<40> bits;
		int Sample();
    };
}
