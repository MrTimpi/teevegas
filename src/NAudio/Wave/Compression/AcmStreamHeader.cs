using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave.MmeInterop;

namespace NAudio.Wave.Compression
{
	internal class AcmStreamHeader : IDisposable
	{
		private readonly IntPtr streamHandle;
		private readonly AcmStreamHeaderStruct streamHeader;
		private byte[] destBuffer;
		private bool disposed;
		private bool firstTime;
		private GCHandle hDestBuffer;
		private GCHandle hSourceBuffer;
		private byte[] sourceBuffer;

		public AcmStreamHeader(IntPtr streamHandle, int sourceBufferLength, int destBufferLength)
		{
			streamHeader = new AcmStreamHeaderStruct();
			sourceBuffer = new byte[sourceBufferLength];
			hSourceBuffer = GCHandle.Alloc(sourceBuffer, GCHandleType.Pinned);

			destBuffer = new byte[destBufferLength];
			hDestBuffer = GCHandle.Alloc(destBuffer, GCHandleType.Pinned);

			this.streamHandle = streamHandle;
			firstTime = true;
			//Prepare();
		}

		public byte[] SourceBuffer
		{
			get { return sourceBuffer; }
		}

		public byte[] DestBuffer
		{
			get { return destBuffer; }
		}

		#region IDisposable Members

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}

		#endregion

		private void Prepare()
		{
			streamHeader.cbStruct = Marshal.SizeOf(streamHeader);
			streamHeader.sourceBufferLength = sourceBuffer.Length;
			streamHeader.sourceBufferPointer = hSourceBuffer.AddrOfPinnedObject();
			streamHeader.destBufferLength = destBuffer.Length;
			streamHeader.destBufferPointer = hDestBuffer.AddrOfPinnedObject();
			MmException.Try(AcmInterop.acmStreamPrepareHeader(streamHandle, streamHeader, 0), "acmStreamPrepareHeader");
		}

		private void Unprepare()
		{
			streamHeader.sourceBufferLength = sourceBuffer.Length;
			streamHeader.sourceBufferPointer = hSourceBuffer.AddrOfPinnedObject();
			streamHeader.destBufferLength = destBuffer.Length;
			streamHeader.destBufferPointer = hDestBuffer.AddrOfPinnedObject();

			MmResult result = AcmInterop.acmStreamUnprepareHeader(streamHandle, streamHeader, 0);
			if (result != MmResult.NoError)
			{
				//if (result == MmResult.AcmHeaderUnprepared)
				throw new MmException(result, "acmStreamUnprepareHeader");
			}
		}

		public int Convert(int bytesToConvert, out int sourceBytesConverted)
		{
			Prepare();
			try
			{
				streamHeader.sourceBufferLength = bytesToConvert;
				streamHeader.sourceBufferLengthUsed = bytesToConvert;
				AcmStreamConvertFlags flags = firstTime
				                              	? (AcmStreamConvertFlags.Start | AcmStreamConvertFlags.BlockAlign)
				                              	: AcmStreamConvertFlags.BlockAlign;
				MmException.Try(AcmInterop.acmStreamConvert(streamHandle, streamHeader, flags), "acmStreamConvert");
				firstTime = false;
				Debug.Assert(streamHeader.destBufferLength == destBuffer.Length, "Codecs should not change dest buffer length");
				sourceBytesConverted = streamHeader.sourceBufferLengthUsed;
			}
			finally
			{
				Unprepare();
			}

			return streamHeader.destBufferLengthUsed;
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				//Unprepare();
				sourceBuffer = null;
				destBuffer = null;
				hSourceBuffer.Free();
				hDestBuffer.Free();
			}
			disposed = true;
		}

		~AcmStreamHeader()
		{
			Debug.Assert(false, "AcmStreamHeader dispose was not called");
			Dispose(false);
		}
	}
}