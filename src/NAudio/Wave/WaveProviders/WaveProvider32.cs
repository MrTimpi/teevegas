﻿using NAudio.Wave.WaveFormats;
using NAudio.Wave.WaveOutputs;

namespace NAudio.Wave.WaveProviders
{
	/// <summary>
	/// Base class for creating a 32 bit floating point wave provider
	/// </summary>
	public abstract class WaveProvider32 : IWaveProvider
	{
		private WaveFormat waveFormat;

		/// <summary>
		/// Initializes a new instance of the WaveProvider32 class 
		/// defaulting to 44.1kHz mono
		/// </summary>
		public WaveProvider32()
			: this(44100, 1)
		{
		}

		/// <summary>
		/// Initializes a new instance of the WaveProvider32 class with the specified
		/// sample rate and number of channels
		/// </summary>
		public WaveProvider32(int sampleRate, int channels)
		{
			SetWaveFormat(sampleRate, channels);
		}

		#region IWaveProvider Members

		/// <summary>
		/// Implements the Read method of IWaveProvider by delegating to the abstract
		/// Read method taking a float array
		/// </summary>
		public int Read(byte[] buffer, int offset, int count)
		{
			var waveBuffer = new WaveBuffer(buffer);
			int samplesRequired = count/4;
			int samplesRead = Read(waveBuffer.FloatBuffer, offset/4, samplesRequired);
			return samplesRead*4;
		}

		/// <summary>
		/// The Wave Format
		/// </summary>
		public WaveFormat WaveFormat
		{
			get { return waveFormat; }
		}

		#endregion

		/// <summary>
		/// Allows you to specify the sample rate and channels for this WaveProvider
		/// (should be initialised before you pass it to a wave player)
		/// </summary>
		public void SetWaveFormat(int sampleRate, int channels)
		{
			waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
		}

		/// <summary>
		/// Method to override in derived classes
		/// Supply the requested number of samples into the buffer
		/// </summary>
		public abstract int Read(float[] buffer, int offset, int sampleCount);
	}
}