using System;
using System.IO;
using NAudio.Wave.WaveFormats;
using NAudio.Wave.WaveStreams;

namespace NAudio.FileFormats.Mp3
{
	/// <summary>
	/// Represents an MP3 Frame
	/// </summary>
	public class Mp3Frame
	{
		private const int MaxFrameLength = 16*1024;

		private static readonly int[,,] bitRates = new[,,]
		                                           	{
		                                           		{
		                                           			// MPEG Version 1
		                                           			{0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448},
		                                           			// Layer 1
		                                           			{0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384},
		                                           			// Layer 2
		                                           			{0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320},
		                                           			// Layer 3
		                                           		},
		                                           		{
		                                           			// MPEG Version 2 & 2.5
		                                           			{0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256},
		                                           			// Layer 1
		                                           			{0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160},
		                                           			// Layer 2 
		                                           			{0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160},
		                                           			// Layer 3 (same as layer 2)
		                                           		}
		                                           	};

		private static readonly int[,] samplesPerFrame = new[,]
		                                                 	{
		                                                 		{
		                                                 			// MPEG Version 1
		                                                 			384, // Layer1
		                                                 			1152, // Layer2
		                                                 			1152 // Layer3
		                                                 		},
		                                                 		{
		                                                 			// MPEG Version 2 & 2.5
		                                                 			384, // Layer1
		                                                 			1152, // Layer2
		                                                 			576 // Layer3
		                                                 		}
		                                                 	};

		private static readonly int[] sampleRatesVersion1 = new[] {44100, 48000, 32000};
		private static readonly int[] sampleRatesVersion2 = new[] {22050, 24000, 16000};
		private static readonly int[] sampleRatesVersion25 = new[] {11025, 12000, 8000};
		private readonly byte[] rawData;

		private int bitRate;
		private ChannelMode channelMode;
		private bool crcPresent;
		//private short crc;
		private int frameLengthInBytes;
		private MpegLayer layer;
		private MpegVersion mpegVersion;
		private int sampleRate;
		private int samplesInFrame; // number of samples in this frame

		/// <summary>Reads an MP3Frame from a stream</summary>
		/// <remarks>http://mpgedit.org/mpgedit/mpeg_format/mpeghdr.htm has some good info
		/// also see http://www.codeproject.com/KB/audio-video/mpegaudioinfo.aspx
		/// </remarks>
		public Mp3Frame(Stream input)
			: this(input, true)
		{
		}


		/// <summary>Reads an MP3Frame from a stream</summary>
		/// <remarks>http://mpgedit.org/mpgedit/mpeg_format/mpeghdr.htm 
		/// has some good info</remarks>
		/// <exception cref="EndOfStreamException">Thrown when we reach the end of the stream without reading a valid frame</exception>
		public Mp3Frame(Stream input, bool readData)
		{
			var reader = new BinaryReader(input);
			// try for a header
			long headerStartPosition = input.Position;
			byte[] headerBytes = reader.ReadBytes(4);
			if (headerBytes.Length < 4)
			{
				throw new EndOfStreamException();
			}

			// Added -jam to play wrapped mp3 files via RIFF
			headerBytes = CheckForRiff(input, reader, headerBytes);

			while (!IsValidHeader(headerBytes))
			{
				//headerStartPosition++;
				byte extra = reader.ReadByte(); // n.b can throw an end of stream exception
				// shift down by one and try again
				headerBytes[0] = headerBytes[1];
				headerBytes[1] = headerBytes[2];
				headerBytes[2] = headerBytes[3];
				headerBytes[3] = extra;
			}
			/* no longer read the CRC since we include this in framelengthbytes
            if (this.crcPresent)
                this.crc = reader.ReadInt16();*/

			rawData = new byte[frameLengthInBytes];
			Array.Copy(headerBytes, rawData, 4);
			int bytesRead = reader.Read(rawData, 4, frameLengthInBytes - 4);
			if (bytesRead < frameLengthInBytes - 4)
			{
				throw new EndOfStreamException();
			}
		}

		/// <summary>
		/// Sample rate of this frame
		/// </summary>
		public int SampleRate
		{
			get { return sampleRate; }
		}

		/// <summary>
		/// Frame length in bytes
		/// </summary>
		public int FrameLength
		{
			get { return frameLengthInBytes; }
		}

		/// <summary>
		/// Bit Rate
		/// </summary>
		public int BitRate
		{
			get { return bitRate; }
		}

		/// <summary>
		/// Raw frame data
		/// </summary>
		public byte[] RawData
		{
			get { return rawData; }
		}

		/// <summary>
		/// MPEG Version
		/// </summary>
		public MpegVersion MpegVersion
		{
			get { return mpegVersion; }
		}

		/// <summary>
		/// MPEG Layer
		/// </summary>
		public MpegLayer MpegLayer
		{
			get { return layer; }
		}

		/// <summary>
		/// Channel Mode
		/// </summary>
		public ChannelMode ChannelMode
		{
			get { return channelMode; }
		}

		/// <summary>
		/// The number of samples in this frame
		/// </summary>
		public int SampleCount
		{
			get { return samplesInFrame; }
		}

		/// <summary>checks if the four bytes represent a valid header,
		/// if they are, will parse the values into local properties
		/// </summary>
		private bool IsValidHeader(byte[] headerBytes)
		{
			if ((headerBytes[0] == 0xFF) && ((headerBytes[1] & 0xE0) == 0xE0))
			{
				// TODO: could do with a bitstream class here
				mpegVersion = (MpegVersion) ((headerBytes[1] & 0x18) >> 3);
				if (mpegVersion == MpegVersion.Reserved)
				{
					//throw new FormatException("Unsupported MPEG Version");
					return false;
				}

				layer = (MpegLayer) ((headerBytes[1] & 0x06) >> 1);

				if (layer == MpegLayer.Reserved)
				{
					return false;
				}
				int layerIndex = layer == MpegLayer.Layer1 ? 0 : layer == MpegLayer.Layer2 ? 1 : 2;
				crcPresent = (headerBytes[1] & 0x01) == 0x00;
				int bitRateIndex = (headerBytes[2] & 0xF0) >> 4;
				if (bitRateIndex == 15)
				{
					// invalid index
					return false;
				}
				int versionIndex = mpegVersion == MpegVersion.Version1 ? 0 : 1;
				bitRate = bitRates[versionIndex, layerIndex, bitRateIndex]*1000;
				if (bitRate == 0)
				{
					return false;
				}
				int sampleFrequencyIndex = (headerBytes[2] & 0x0C) >> 2;
				if (sampleFrequencyIndex == 3)
				{
					return false;
				}
				if (mpegVersion == MpegVersion.Version1)
					sampleRate = sampleRatesVersion1[sampleFrequencyIndex];
				else if (mpegVersion == MpegVersion.Version2)
					sampleRate = sampleRatesVersion2[sampleFrequencyIndex];
				else // mpegVersion == MpegVersion.Version25
					sampleRate = sampleRatesVersion25[sampleFrequencyIndex];

				bool padding = (headerBytes[2] & 0x02) == 0x02;
				bool privateBit = (headerBytes[2] & 0x01) == 0x01;
				channelMode = (ChannelMode) ((headerBytes[3] & 0xC0) >> 6);
				int channelExtension = (headerBytes[3] & 0x30) >> 4;
				bool copyright = (headerBytes[3] & 0x08) == 0x08;
				bool original = (headerBytes[3] & 0x04) == 0x04;
				int emphasis = (headerBytes[3] & 0x03);

				int nPadding = padding ? 1 : 0;

				samplesInFrame = samplesPerFrame[versionIndex, layerIndex];
				int coefficient = samplesInFrame/8;
				if (layer == MpegLayer.Layer1)
				{
					frameLengthInBytes = (coefficient*bitRate/sampleRate + nPadding)*4;
				}
				else
				{
					frameLengthInBytes = (coefficient*bitRate)/sampleRate + nPadding;
				}

				if (frameLengthInBytes > MaxFrameLength)
				{
					return false;
				}
				return true;
			}
			return false;
		}

		private static byte[] CheckForRiff(Stream input, BinaryReader reader, byte[] headerBytes)
		{
			if ((headerBytes[0] == 'R') &&
			    (headerBytes[1] == 'I') &&
			    (headerBytes[2] == 'F') &&
			    (headerBytes[3] == 'F'))
			{
				// Backup 4 bytes
				input.Position -= 4;

				// Now start parsing
				WaveFormat format;
				long dataChunkPosition;
				int dataChunkLength;

				WaveFileReader.ReadWaveHeader(input, out format, out dataChunkPosition, out dataChunkLength, null);

				// Now read the actual mp3 header
				input.Position = dataChunkPosition;
				headerBytes = reader.ReadBytes(4);
			}
			return headerBytes;
		}
	}
}