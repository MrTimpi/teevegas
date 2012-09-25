using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi.Interfaces;

namespace NAudio.CoreAudioApi
{
	// This class implements the IAudioEndpointVolumeCallback interface,
	// it is implemented in this class because implementing it on AudioEndpointVolume 
	// (where the functionality is really wanted, would cause the OnNotify function 
	// to show up in the public API. 
	internal class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
	{
		private readonly AudioEndpointVolume _Parent;

		internal AudioEndpointVolumeCallback(AudioEndpointVolume parent)
		{
			_Parent = parent;
		}

		#region IAudioEndpointVolumeCallback Members

		public int OnNotify(IntPtr NotifyData)
		{
			//Since AUDIO_VOLUME_NOTIFICATION_DATA is dynamic in length based on the
			//number of audio channels available we cannot just call PtrToStructure 
			//to get all data, thats why it is split up into two steps, first the static
			//data is marshalled into the data structure, then with some IntPtr math the
			//remaining floats are read from memory.
			//
			var data =
				(AudioVolumeNotificationDataStruct) Marshal.PtrToStructure(NotifyData, typeof (AudioVolumeNotificationDataStruct));

			//Determine offset in structure of the first float
			IntPtr Offset = Marshal.OffsetOf(typeof (AudioVolumeNotificationDataStruct), "ChannelVolume");
			//Determine offset in memory of the first float
			var FirstFloatPtr = (IntPtr) ((long) NotifyData + (long) Offset);

			var voldata = new float[data.nChannels];

			//Read all floats from memory.
			for (int i = 0; i < data.nChannels; i++)
			{
				voldata[i] = (float) Marshal.PtrToStructure(FirstFloatPtr, typeof (float));
			}

			//Create combined structure and Fire Event in parent class.
			var NotificationData = new AudioVolumeNotificationData(data.guidEventContext, data.bMuted, data.fMasterVolume,
			                                                       voldata);
			_Parent.FireNotification(NotificationData);
			return 0; //S_OK
		}

		#endregion
	}
}