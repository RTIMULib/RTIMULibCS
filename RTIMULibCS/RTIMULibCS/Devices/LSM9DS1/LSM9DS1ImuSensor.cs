////////////////////////////////////////////////////////////////////////////
//
//  This file is part of RTIMULibCS
//
//  Copyright (c) 2015, richards-tech, LLC
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy of 
//  this software and associated documentation files (the "Software"), to deal in 
//  the Software without restriction, including without limitation the rights to use, 
//  copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the 
//  Software, and to permit persons to whom the Software is furnished to do so, 
//  subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all 
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
//  INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
//  PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
//  HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
//  OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
//  SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace RichardsTech.Sensors.Devices.LSM9DS1
{
	public class LSM9DS1ImuSensor : ImuSensor
	{
		private readonly byte _accelGyroI2CAddress;
		private readonly byte _magI2CAddress;
		private readonly LSM9DS1Config _config;
		private I2cDevice _accelGyroI2CDevice;
		private I2cDevice _magI2CDevice;

		public LSM9DS1ImuSensor(byte accelGyroI2CAddress, byte magI2CAddress, LSM9DS1Config config)
		{
			_accelGyroI2CAddress = accelGyroI2CAddress;
			_magI2CAddress = magI2CAddress;
			_config = config;
			SampleRate = 100;
		}

		protected override async Task<bool> InitDeviceAsync()
		{
			await ConnectToI2CDevices();

			await BootDevice();

			VerifyDeviceAccelGyroId();
			SetGyroSampleRate();
			SetGyroCtrl3();
			VerifyDeviceMagId();
			SetAccelCtrl6();
			SetAccelCtrl7();
			SetMagCtrl1();
			SetMagCtrl2();
			SetMagCtrl3();

			return true;
		}

		private async Task ConnectToI2CDevices()
		{
			try
			{
				string aqsFilter = I2cDevice.GetDeviceSelector("I2C1");

				DeviceInformationCollection collection = await DeviceInformation.FindAllAsync(aqsFilter);
				if (collection.Count == 0)
				{
					throw new SensorException("I2C device not found");
				}

				I2cConnectionSettings accelGyroI2CSettings = new I2cConnectionSettings(_accelGyroI2CAddress)
				{
					BusSpeed = I2cBusSpeed.FastMode
				};

				_accelGyroI2CDevice = await I2cDevice.FromIdAsync(collection[0].Id, accelGyroI2CSettings);

				I2cConnectionSettings magI2CSettings = new I2cConnectionSettings(_magI2CAddress)
				{
					BusSpeed = I2cBusSpeed.FastMode
				};

				_magI2CDevice = await I2cDevice.FromIdAsync(collection[0].Id, magI2CSettings);
			}
			catch (Exception exception)
			{
				throw new SensorException("Failed to connect to LSM9DS1", exception);
			}
		}

		private async Task BootDevice()
		{
			if (!I2CSupport.Write(_accelGyroI2CDevice, LSM9DS1Defines.CTRL8, 0x81))
			{
				throw new SensorException("Failed to boot LSM9DS1");
			}

			await Task.Delay(100);
		}

		private void VerifyDeviceAccelGyroId()
		{
			byte[] oneByte = new byte[1];

			if (!I2CSupport.Read(_accelGyroI2CDevice, LSM9DS1Defines.WHO_AM_I, oneByte))
			{
				throw new SensorException("Failed to read LSM9DS1 accel/gyro id");
			}

			if (oneByte[0] != LSM9DS1Defines.ID)
			{
				throw new SensorException($"Incorrect LSM9DS1 gyro id {oneByte[0]}");
			}
		}

		private void VerifyDeviceMagId()
		{
			byte[] oneByte = new byte[1];

			if (!I2CSupport.Read(_magI2CDevice, LSM9DS1Defines.MAG_WHO_AM_I, oneByte))
			{
				throw new SensorException("Failed to read LSM9DS1 mag id");
			}

			if (oneByte[0] != LSM9DS1Defines.MAG_ID)
			{
				throw new SensorException($"Incorrect LSM9DS1 mag id {oneByte[0]}");
			}
		}

		private void SetGyroSampleRate()
		{
			byte ctrl1;

			switch (_config.GyroSampleRate)
			{
				case GyroSampleRate.Freq14_9Hz:
					ctrl1 = 0x20;
					SampleRate = 15;
					break;

				case GyroSampleRate.Freq59_5Hz:
					ctrl1 = 0x40;
					SampleRate = 60;
					break;

				case GyroSampleRate.Freq119Hz:
					ctrl1 = 0x60;
					SampleRate = 119;
					break;

				case GyroSampleRate.Freq238Hz:
					ctrl1 = 0x80;
					SampleRate = 238;
					break;

				case GyroSampleRate.Freq476Hz:
					ctrl1 = 0xa0;
					SampleRate = 476;
					break;

				case GyroSampleRate.Freq952Hz:
					ctrl1 = 0xc0;
					SampleRate = 952;
					break;

				default:
					throw new SensorException($"Illegal LSM9DS1 gyro sample rate code {_config.GyroSampleRate}");
			}

			SampleInterval = (long)1000000 / SampleRate;

			switch (_config.GyroBandwidthCode)
			{
				case GyroBandwidthCode.BandwidthCode0:
					ctrl1 |= 0x00;
					break;

				case GyroBandwidthCode.BandwidthCode1:
					ctrl1 |= 0x01;
					break;

				case GyroBandwidthCode.BandwidthCode2:
					ctrl1 |= 0x02;
					break;

				case GyroBandwidthCode.BandwidthCode3:
					ctrl1 |= 0x03;
					break;

				default:
					throw new SensorException($"Illegal LSM9DS1 gyro BW code {_config.GyroBandwidthCode}");
			}

			switch (_config.GyroFullScaleRange)
			{
				case GyroFullScaleRange.Range250:
					ctrl1 |= 0x00;
					GyroScale = 0.00875 * MathSupport.DegreeToRad;
					break;

				case GyroFullScaleRange.Range500:
					ctrl1 |= 0x08;
					GyroScale = 0.0175 * MathSupport.DegreeToRad;
					break;

				case GyroFullScaleRange.Range2000:
					ctrl1 |= 0x18;
					GyroScale = 0.07 * MathSupport.DegreeToRad;
					break;

				default:
					throw new SensorException($"Illegal LSM9DS1 gyro FSR code {_config.GyroFullScaleRange}");
			}

			if (!I2CSupport.Write(_accelGyroI2CDevice, LSM9DS1Defines.CTRL1, ctrl1))
			{
				throw new SensorException("Failed to set LSM9DS1 gyro CTRL1");
			}
		}

		private void SetGyroCtrl3()
		{
			int gyroHighPassFilterCodeValue = (int)_config.GyroHighPassFilterCode;
			if ((gyroHighPassFilterCodeValue < 0) || (gyroHighPassFilterCodeValue > 9))
			{
				throw new SensorException($"Illegal LSM9DS1 gyro high pass filter code {_config.GyroHighPassFilterCode}");
			}

			byte ctrl3 = (byte)gyroHighPassFilterCodeValue;

			//  Turn on hpf
			ctrl3 |= 0x40;

			if (!I2CSupport.Write(_accelGyroI2CDevice, LSM9DS1Defines.CTRL3, ctrl3))
			{
				throw new SensorException("Failed to set LSM9DS1 gyro CTRL3");
			}
		}

		private void SetAccelCtrl6()
		{
			int accelSampleRateValue = (int)_config.AccelSampleRate;
			if ((accelSampleRateValue < 0) || (accelSampleRateValue > 6)) // TODO: Isn't the lower bound 1 (not 0)?
			{
				throw new SensorException($"Illegal LSM9DS1 accel sample rate code {_config.AccelSampleRate}");
			}

			int accelLowPassFilterValue = (int)_config.AccelLowPassFilter;
			if ((accelLowPassFilterValue < 0) || (accelLowPassFilterValue > 3))
			{
				throw new SensorException($"Illegal LSM9DS1 accel low pass fiter code {_config.AccelLowPassFilter}");
			}

			byte ctrl6 = (byte)(accelSampleRateValue << 5);

			switch (_config.AccelFullScaleRange)
			{
				case AccelFullScaleRange.Range2g:
					AccelScale = 0.000061;
					break;

				case AccelFullScaleRange.Range4g:
					AccelScale = 0.000122;
					break;

				case AccelFullScaleRange.Range8g:
					AccelScale = 0.000244;
					break;

				case AccelFullScaleRange.Range16g:
					AccelScale = 0.000732;
					break;

				default:
					throw new SensorException($"Illegal LSM9DS1 accel FSR code {_config.AccelFullScaleRange}");
			}

			ctrl6 |= (byte)((accelLowPassFilterValue) | (accelSampleRateValue << 3));

			if (!I2CSupport.Write(_accelGyroI2CDevice, LSM9DS1Defines.CTRL6, ctrl6))
			{
				throw new SensorException("Failed to set LSM9DS1 accel CTRL6");
			}
		}

		private void SetAccelCtrl7()
		{
			byte ctrl7;

			ctrl7 = 0x00;
			//Bug: Bad things happen.
			//ctrl7 = 0x05;

			if (!I2CSupport.Write(_accelGyroI2CDevice, LSM9DS1Defines.CTRL7, ctrl7))
			{
				throw new SensorException("Failed to set LSM9DS1 accel CTRL7");
			}
		}

		private void SetMagCtrl1()
		{
			int compassSampleRateValue = (int)_config.CompassSampleRate;

			if ((compassSampleRateValue < 0) || (compassSampleRateValue > 5)) // TODO: Only 5?
			{
				throw new SensorException($"Illegal LSM9DS1 compass sample rate code {_config.CompassSampleRate}");
			}

			byte ctrl1 = (byte)(compassSampleRateValue << 2);

			if (!I2CSupport.Write(_magI2CDevice, LSM9DS1Defines.MAG_CTRL1, ctrl1))
			{
				throw new SensorException("Failed to set LSM9DS1 compass CTRL5");
			}
		}

		private void SetMagCtrl2()
		{
			byte ctrl2;

			//  convert FSR to uT

			switch (_config.MagneticFullScaleRange)
			{
				case MagneticFullScaleRange.Range4Gauss:
					ctrl2 = 0;
					MagScale = 0.014;
					break;

				case MagneticFullScaleRange.Range8Gauss:
					ctrl2 = 0x20;
					MagScale = 0.029;
					break;

				case MagneticFullScaleRange.Range12Gauss:
					ctrl2 = 0x40;
					MagScale = 0.043;
					break;

				case MagneticFullScaleRange.Range16Gauss:
					ctrl2 = 0x60;
					MagScale = 0.058;
					break;

				default:
					throw new SensorException($"Illegal LSM9DS1 compass FSR code {_config.MagneticFullScaleRange}");
			}

			if (!I2CSupport.Write(_magI2CDevice, LSM9DS1Defines.MAG_CTRL2, ctrl2))
			{
				throw new SensorException("Failed to set LSM9DS1 compass CTRL6");
			}
		}

		private void SetMagCtrl3()
		{
			if (!I2CSupport.Write(_magI2CDevice, LSM9DS1Defines.MAG_CTRL3, 0x00))
			{
				throw new SensorException("Failed to set LSM9DS1 compass CTRL3");
			}
		}

		public int GetPollInterval()
		{
			return (400 / SampleRate);
		}

		public override bool Update()
		{
			byte[] status = new byte[1];
			if (!I2CSupport.Read(_accelGyroI2CDevice, LSM9DS1Defines.STATUS, status))
			{
				throw new SensorException("Failed to read LSM9DS1 status");
			}

			if ((status[0] & 0x3) != 3)
			{
				// Data not yet available
				return false;
			}

			byte[] gyroData = new byte[6];
			if (!I2CSupport.Read(_accelGyroI2CDevice, 0x80 + LSM9DS1Defines.OUT_X_L_G, gyroData))
			{
				throw new SensorException("Failed to read LSM9DS1 gyro data");
			}

			byte[] accelData = new byte[6];
			if (!I2CSupport.Read(_accelGyroI2CDevice, 0x80 + LSM9DS1Defines.OUT_X_L_XL, accelData))
			{
				throw new SensorException("Failed to read LSM9DS1 accel data");
			}

			byte[] magData = new byte[6];
			if (!I2CSupport.Read(_magI2CDevice, 0x80 + LSM9DS1Defines.MAG_OUT_X_L, magData))
			{
				throw new SensorException("Failed to read LSM9DS1 compass data");
			}

			var readings = new SensorReadings
			{
				Timestamp = DateTime.Now,
				Gyro = MathSupport.ConvertToVector(gyroData, GyroScale, false),
				GyroValid = true,
				Acceleration = MathSupport.ConvertToVector(accelData, AccelScale, false),
				AccelerationValid = true,
				MagneticField = MathSupport.ConvertToVector(magData, MagScale, false),
				MagneticFieldValid = true
			};

			//  sort out gyro axes and correct for bias

			readings.Gyro.Z = -readings.Gyro.Z;

			//  sort out accel data;

			readings.Acceleration.X = -readings.Acceleration.X;
			readings.Acceleration.Y = -readings.Acceleration.Y;	

			//  sort out mag axes

			readings.MagneticField.X = -readings.MagneticField.X;
			readings.MagneticField.Z = -readings.MagneticField.Z; 

			AssignNewReadings(readings);
			return true;
		}
	}
}