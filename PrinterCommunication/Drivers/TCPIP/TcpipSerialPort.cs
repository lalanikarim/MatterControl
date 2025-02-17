﻿﻿using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MatterHackers.MatterControl.PrinterCommunication;

namespace TcpipDriver
{
	class TcpipSerialPort :IFrostedSerialPort
	{
		// Telnet protocol characters
		const byte IAC = 255;  // escape
		const byte DONT = 254; // negotiation
		const byte DO = 253;// negotiation
		const byte WILL = 251;  // negotiation
		const byte SB = 250;  // subnegotiation begin
		const byte SE = 240;  // subnegotiation end
		const byte ComPortOpt = 44;  // COM port options
		const byte SetBaud = 1;  // Set baud rate
		const byte SetDataSize = 2; // Set data size
		const byte SetParity = 3;  // Set parity
		const byte SetControl = 5;  // Set control lines
		const byte DTR_ON = 8;  // used here to reset microcontroller
		const byte DTR_OFF = 9;
		const byte RTS_ON = 11;  // used here to signal ISP (in-system-programming) to uC
		const byte RTS_OFF = 12;

		private Socket socket;
		private NetworkStream stream;//Seems to have more in common with the socket so we will use to make this interface easier
		private IPAddress ipAddress;
		private int port;
		private IPEndPoint ipEndPoint;
		private byte[] readBuffer;
		private int bufferIndex;

		//These get set before open is called but the stream is not created until open is called. Preserver values to be set after stream is created.
		private int tempReadTimeout;
		private int tempWriteTimeout;

		private bool reconnecting = false;
		PrinterConnection printerConnection;

		public TcpipSerialPort(PrinterConnection printerConnection, string name)
		{
			this.printerConnection = printerConnection;
			socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			if (int.TryParse(ActiveSliceSettings.Instance.GetValue("ip_port"), out port)
				&& IPAddress.TryParse(ActiveSliceSettings.Instance.GetValue("ip_address"), out ipAddress))
			{
				ipEndPoint = new IPEndPoint(ipAddress, port);
				readBuffer = new byte[1024];
				bufferIndex = 0;
			}
			else
			{
				this.IsValid = false;
			}
		}

		public bool IsValid { get; } = true;

		public int BaudRate { get; set; }

		public int BytesToRead
		{
			get
			{
				if(stream.DataAvailable)
				{
					int bytesRead = stream.Read(readBuffer, bufferIndex, readBuffer.Length);
					bufferIndex += bytesRead;
				}

				return bufferIndex;
			}
		}

		public bool DtrEnable
		{
			get { return dtrEnable; }
			set
			{
				if (stream != null)
				{
					SetDtrEnable(value);
				}
				dtrEnable = value;
			}
		}
		private bool dtrEnable;

		// Eventually I will need to find out how to check that the port is open and connectable
		public bool IsOpen { get; } = true;

		public int ReadTimeout
		{
			get
			{
				return stream.ReadTimeout;
			}

			set
			{
				if(stream != null)
				{
					stream.ReadTimeout = value;
				}
				else
				{
					tempReadTimeout = value;
				}
			}
		}

		public bool RtsEnable { get; set; }

		public int WriteTimeout
		{
			get
			{
				return stream.WriteTimeout;
			}

			set
			{
				if(stream != null)
				{
					stream.WriteTimeout = value;
				}
				else
				{
					tempWriteTimeout = value;
				}
			}
		}

		public void Close()
		{
			socket.Close();
		}

		public void Dispose()
		{
			stream.Dispose();
		}

		public void Open()
		{
			try
			{//ADD Attempt to connect Message to just the console
				printerConnection.TerminalLog.WriteLine("Attempting to connect to: " + ipEndPoint.Address + " on port " + ipEndPoint.Port);
				socket.Connect(ipEndPoint);
				stream = new NetworkStream(socket);
				printerConnection.TerminalLog.WriteLine("Connected to: " + ipEndPoint.Address + " on port " + ipEndPoint.Port);
				if (this.BaudRate != 0)
				{
					//Send Telnet handshake so that esp will enter the telnet mode allowing us to set baud and reset board
					byte[] bytes = new byte[] { IAC, WILL, ComPortOpt };
					Write(bytes, 0, bytes.Length);
					//Set baud and reset board
					SetBaudRate(this.BaudRate);
				}
			}
			catch (Exception e)
			{//ADD Error Message to just the console
				printerConnection.TerminalLog.WriteLine("Exception:" + e.Message);
			}

			//These were set before and are now set in the stream
			// if (stream != null)
			{
				stream.WriteTimeout = tempWriteTimeout;
				stream.ReadTimeout = tempReadTimeout;
			}
		}

		public int Read(byte[] buffer, int offset, int count)
		{
			Array.Copy(readBuffer, offset, buffer, 0, count);
			Array.Clear(buffer, 0, count);
			bufferIndex -= count;
			Array.Copy(readBuffer, count, readBuffer, 0, bufferIndex);//THis may throw an exception as the target and source are the same

			return count;
		}

		public string ReadExisting()
		{
			string bufferAsString = ConvertBytesToString(readBuffer, bufferIndex);
			Array.Clear(readBuffer, 0, bufferIndex);
			bufferIndex = 0;
			return bufferAsString;
		}

		public void Write(string str)
		{
			var buffer = ConvertStringToBytes(str);
			Write(buffer, 0, buffer.Length);
		}

		public void Write(byte[] buffer, int offset, int count)
		{
			if (!reconnecting)
			{
				try
				{
					stream.Write(buffer, offset, count);
				}
				catch (Exception e)
				{
					printerConnection.TerminalLog.WriteLine("Exception:" + e.Message);
					Reconnect();
					stream.Write(buffer, offset, count);
				}
			}
		}

		private static byte[] ConvertStringToBytes(string str)
		{
			byte[] bytes = new byte[str.Length];
			for (int i = 0; i < str.Length; i++)
			{
				bytes[i] = Convert.ToByte(str[i]);
			}

			return bytes;
		}

		private string ConvertBytesToString(byte[] inputBytes, int bytesRead)
		{
			StringBuilder builder = new StringBuilder();

			for (int index = 0; index < bytesRead; index++)
			{
				builder.Append(Convert.ToChar(inputBytes[index]));
			}
			return builder.ToString();
		}

		private void Reconnect()
		{
			reconnecting = true;
			try
			{
				socket?.Close();
			}
			catch{ }

			for(int i =0; i< 5; i++)
			{
				ipEndPoint = new IPEndPoint(ipAddress, port);
				socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				try
				{//ADD Attempt to connect Message to just the console
					printerConnection.TerminalLog.WriteLine("Attempting to connect to: " + ipEndPoint.Address + " on port " + ipEndPoint.Port);
					socket.Connect(ipEndPoint);
					stream = new NetworkStream(socket);
					printerConnection.TerminalLog.WriteLine("Connected to: " + ipEndPoint.Address + " on port " + ipEndPoint.Port);
					//Send telnet handshake
					byte[] bytes = new byte[] { IAC, WILL, ComPortOpt };
					Write(bytes, 0, bytes.Length);
					break;
				}
				catch (Exception e)
				{//ADD Error Message to just the console
					printerConnection.TerminalLog.WriteLine("Exception:" + e.Message);
					Thread.Sleep((int)(500 * Math.Pow(i,2)));
				}
			}

			reconnecting = false;
		}

		private void SetDtrEnable(bool dtr)
		{
			byte dtrEnabled = dtr ? DTR_ON : DTR_OFF;

			//Create Sequence of bytes that will cause board to be reset
			byte[] bytes = new byte[] { IAC, SB, ComPortOpt, SetControl, dtrEnabled, IAC, SE };

			Write(bytes, 0, bytes.Length);
		}

		private void SetBaudRate(int baudRate)
		{
			byte[] baudBytes = BitConverter.GetBytes(baudRate);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(baudBytes);
			}
			//Create Sequence of bytes that will set baudrate
			byte[] bytes = new byte[] { IAC, SB, ComPortOpt, SetBaud, baudBytes[0], baudBytes[1], baudBytes[2], baudBytes[3], IAC, SE };

			Write(bytes, 0, bytes.Length);
		}
	}
}
