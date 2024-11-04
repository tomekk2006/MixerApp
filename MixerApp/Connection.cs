using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Ports;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using ABI.Microsoft.UI.Xaml.Controls;

namespace MixerApp
{
	public class Connection : SerialPort
	{
		public bool IsRunning { get; set; }
		public ObservableCollection<Slider> Sliders { set; get; }
		private DispatcherQueue dispatcher;
		private Thread readThread;
		public List<AppStorage.JsonAudioControl> ControlQueue { get; set; }
		public int JumpDistance { get; set; }
		public Connection()
		{
			IsRunning = false;
			dispatcher = DispatcherQueue.GetForCurrentThread();
			ControlQueue = new List<AppStorage.JsonAudioControl>();

		}
		// This is a loop that asigns values from serial connection to each slider
		// Runs on a separate thread
		private void Read()
		{
			int sliderCount = Sliders.Count; // counts how many sliders there are
											 // main loop
			while (IsRunning)
			{
				string line;
				try
				{
					DiscardInBuffer();
					line = ReadLine();
				}
				// if device disconnects
				catch (OperationCanceledException)
				{
					Stop();
					continue;
				}
				// ignores time out
				catch (TimeoutException)
				{
					continue;
				}
				List<string> values = line.Split("|").ToList(); // splits values
																// for each slider update the value from each integer number in values
				foreach (Slider slider in Sliders)
				{
					int result;
					bool status = false;
					try
					{
						status = Int32.TryParse(values.First(), out result); // convert string to int
					}
					catch (InvalidOperationException)
					{
						Debug.WriteLine("(No Values In List Error) Printed line: " + line);
						break;
					}

					if (status == false)
					{
						values.RemoveAt(0);
						break;
					}
					// executes in ui thread to update the values on window
					bool Updated = slider.UpdateValue(result);

					if (Updated)
					{
						slider.UpdateVolumes();
						dispatcher.TryEnqueue(() =>
						{
							slider.OnPropertyChanged("SliderValue");
						});
					}
					values.RemoveAt(0);
				}
			}
			Close();
		}


		// main start function
		public bool Start()
		{
			// tries to open connection
			try
			{
				Open();
			}
			// connection blocked exception
			catch (UnauthorizedAccessException)
			{
				return false;
			}
			catch (FileNotFoundException)
			{
				return false;
			}
			catch (IOException)
			{
				return false;
			}
			Sliders = new ObservableCollection<Slider>();
			string initLine;
			// tries to read a line
			try
			{
				initLine = ReadLine();
			}
			// when you can't read the line return false
			catch (TimeoutException)
			{
				return false;
			}
			// split each value and check if value is an integer
			// if true then create a new slider
			// otherwise return false and end connection
			string[] splitLine = initLine.Split("|");
			int index = 1;
			foreach (string line in splitLine)
			{
				int Number;
				if (Int32.TryParse(line, out Number) == true)
				{
					Sliders.Add(new Slider(this)
					{
						ParentConnection = this,
						SliderValue = Number,
						Name = "Slider " + index.ToString(),
					});
					index++;
				}
				else
				{
					return false;
				}

			}
			foreach (AppStorage.JsonAudioControl control in ControlQueue) {
				Slider slider;
				try
				{
					slider = Sliders[control.id];
				}
				catch (IndexOutOfRangeException) { 
					continue;
				}
				slider.AudioControls.Add(new AudioControl((AudioControl.Type)control.type, slider)
				{
					Mapping = control.mapping,
				});

			}
			// start the main loop and return true for successful connection
			IsRunning = true;
			readThread = new Thread(Read);
			readThread.IsBackground = true;
			readThread.Start();
			return true;
		}
		// ends loop and closes connection
		public void Stop()
		{
			if (IsRunning)
			{
				IsRunning = false;
				readThread.Join();
				Close();
			}

		}
		public Collection<AudioControl> GetAudioControls(AudioControl.Type? filter = null)
		{
			Collection<AudioControl> controls = new Collection<AudioControl>();
			foreach (Slider slider in Sliders)
			{
				foreach (AudioControl audioControl in slider.AudioControls)
				{
					if (filter != null && audioControl.SliderType == filter)
					{
						controls.Add(audioControl);
					}
					else
					{
						controls.Add(audioControl);
					}

				}
			}
			return controls;
		}
	}
}
