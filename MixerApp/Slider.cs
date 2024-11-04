using CoreAudio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MixerApp
{
	public class Slider : INotifyPropertyChanged
	{
		public int SliderValue { get; set; }// slider percentage: 0 to 100
		private int RawValue { get; set; } // raw value from 0 to 1024
		public Connection ParentConnection { get; set; }
		public string Name { get; set; }
		public ObservableCollection<AudioControl> AudioControls { get; set; }
		public event PropertyChangedEventHandler PropertyChanged = delegate { };
		public string ControlsText { get { return AudioControls.Count.ToString() + " controls"; } }

		public Slider(Connection Parent)
		{
			ParentConnection = Parent;
			SliderValue = 0;
			RawValue = 0;

			AudioControls = new ObservableCollection<AudioControl>();
		}

		public bool UpdateValue(int NewRaw)
		{
			int Distance = Math.Abs(NewRaw - RawValue);
			// this function updates the sliderValue only when the value changes
			if (Distance > ParentConnection.JumpDistance)
			{
				RawValue = NewRaw;
				double floatRaw = (double)RawValue;
				SliderValue = (int)(floatRaw / 1024.0 * 100.0);
				return true;
			}
			else
			{
				return false;
			}
		}
		// change volume of all audiocontrols in slider
		public void UpdateVolumes()
		{
			foreach (AudioControl audioControl in AudioControls)
			{

				switch (audioControl.SliderType)
				{

					case AudioControl.Type.SPEAKER:
						{
							if (audioControl.Mapping == "")
							{
								MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
								MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
								device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)SliderValue / 100.0f;
							}
							else
							{
								MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
								if (audioControl.AllowSearch == true)
								{
									audioControl.AllowSearch = false;
									foreach (MMDevice device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
									{
										if (device.DeviceInterfaceFriendlyName == audioControl.Mapping)
										{
											audioControl.DeviceId = device.ID;
											device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)SliderValue / 100.0f;
											break;
										}
									}
								}
								else if (audioControl.DeviceId != null)
								{
									MMDevice device = deviceEnumerator.GetDevice(audioControl.DeviceId);
									try
									{
										device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)SliderValue / 100.0f;
									}
									catch (COMException)
									{
										continue;
									}

								}
							}
							break;
						}
					case AudioControl.Type.MICROPHONE:
						{
							if (audioControl.Mapping == "")
							{
								MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
								MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
								device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)SliderValue / 100.0f;
							}
							else
							{
								MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
								if (audioControl.AllowSearch == true)
								{
									audioControl.AllowSearch = false;
									foreach (MMDevice device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
									{
										if (device.DeviceInterfaceFriendlyName == audioControl.Mapping)
										{
											audioControl.DeviceId = device.ID;
											device.AudioEndpointVolume.MasterVolumeLevel = (float)SliderValue / 100.0f;
											break;
										}
									}
								}
								else if (audioControl.DeviceId != null)
								{
									MMDevice device = deviceEnumerator.GetDevice(audioControl.DeviceId);
									device.AudioEndpointVolume.MasterVolumeLevel = (float)SliderValue / 100.0f;
								}
							}
							break;
						}
					case AudioControl.Type.SYSTEM:
						{
							MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
							MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

							foreach (AudioSessionControl2 audioSession in device.AudioSessionManager2.Sessions)
							{
								if (audioSession.IsSystemSoundsSession)
								{
									audioSession.SimpleAudioVolume.MasterVolume = (float)SliderValue / 100.0f;
									break;
								}
							}
							break;
						}
					case AudioControl.Type.PROCESS:
						{
							MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
							MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

							foreach (AudioSessionControl2 audioSession in device.AudioSessionManager2.Sessions)
							{
								int Id = (int)audioSession.ProcessID;
								string Name;
								try
								{
									Name = Process.GetProcessById(Id).ProcessName;
								}
								catch (ArgumentException)
								{
									continue;
								}
								if (audioControl.Mapping.ToLower() == Name.ToLower())
								{
									audioSession.SimpleAudioVolume.MasterVolume = (float)SliderValue / 100.0f;
								}
							}
							break;
						}
					case AudioControl.Type.UNMAPPED:
						{
							Collection<AudioControl> controls = ParentConnection.GetAudioControls(AudioControl.Type.PROCESS);
							MMDeviceEnumerator deviceEnumerator = new MMDeviceEnumerator();
							MMDevice device = deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
							List<uint> SessionIds = new List<uint>();
							//get session id's
							foreach (AudioControl control in controls)
							{
								foreach (AudioSessionControl2 audioSession in device.AudioSessionManager2.Sessions)
								{
									string Name;
									try
									{
										Name = Process.GetProcessById((int)audioSession.ProcessID).ProcessName;
									}
									catch (ArgumentException)
									{
										continue;
									}
									if (control.Mapping.ToLower() == Name.ToLower())
									{
										//Debug.WriteLine(Name + " is mapped");
										SessionIds.Add(audioSession.ProcessID);
									}
								}
							}
							// compare id's
							foreach (AudioSessionControl2 audioSession in device.AudioSessionManager2.Sessions)
							{
								if (SessionIds.Contains(audioSession.ProcessID) == false &&
									audioSession.IsSystemSoundsSession == false)
								{
									audioSession.SimpleAudioVolume.MasterVolume = (float)SliderValue / 100.0f;
								}
							}
							break;
						}
				}
			}
		}
		// templates for controls
		public void CreateProcessControl()
		{
			AudioControls.Insert(0, new AudioControl(AudioControl.Type.PROCESS, this));
			OnPropertyChanged("ControlsText");
		}
		public void CreateUnmappedControl()
		{
			AudioControls.Insert(0, new AudioControl(AudioControl.Type.UNMAPPED, this));
			OnPropertyChanged("ControlsText");
		}
		public void CreateSpeakerControl()
		{
			AudioControls.Insert(0, new AudioControl(AudioControl.Type.SPEAKER, this));
			OnPropertyChanged("ControlsText");
		}
		public void CreateMicrophoneControl()
		{
			AudioControls.Insert(0, new AudioControl(AudioControl.Type.MICROPHONE, this));
			OnPropertyChanged("ControlsText");
		}
		public void CreateSystemControl()
		{
			AudioControls.Insert(0, new AudioControl(AudioControl.Type.SYSTEM, this));
			OnPropertyChanged("ControlsText");
		}
		// event to update bind
		public void OnPropertyChanged(string propertyName = null)
		{
			// Raise the PropertyChanged event, passing the name of the property whose value has changed.
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

	}
}
