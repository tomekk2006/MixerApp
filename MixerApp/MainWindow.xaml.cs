using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using System.IO.Ports;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using System.Linq.Expressions;
using CoreAudio;
using CommunityToolkit.WinUI;
using Windows.Storage.Streams;
using Windows.Media.Audio;
using Windows.Devices.WiFi;
using System.Runtime.InteropServices;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Windows.AppNotifications.Builder;
using Microsoft.Windows.AppNotifications;
using Windows.Storage;
using System.Text.Json;
using Windows.Devices.AllJoyn;


namespace MixerApp
{
	[ObservableObject]
	public sealed partial class MainWindow : Window
	{
		ObservableCollection<Slider> Sliders { get; set; }
		Connection connection { get; set; }
		bool ShuttingDown { get; set; }

		public MainWindow()
		{
			this.InitializeComponent();

			// Set up for New Connection Dialog
			ShuttingDown = false;
			connection = new Connection();
			Sliders = new ObservableCollection<Slider>(); // sliders will apear on screen when connected
			SerialPort defaultSerial = new SerialPort();
			PortComboBox.ItemsSource = SerialPort.GetPortNames();
			PortComboBox.SelectedItem = defaultSerial.PortName;
			BaudRateNumberBox.Value = defaultSerial.BaudRate;

			
		}
		
		// Create a new connection to a serial device
		private async void NewConnectionButton_Click(object sender, RoutedEventArgs e)
		{
			
			// Open dialog
			ConnectionDialog.XamlRoot = rootPanel.XamlRoot;
			ContentDialogResult result = await ConnectionDialog.ShowAsync();
			// When clicked connect
			if (result == ContentDialogResult.Primary)
			{
				NewConnectionButton.IsEnabled = false;
				// Create a serial connection
				try
				{
					connection.Stop(); // stop any existing connection
					connection = new Connection()
					{
						PortName = (string)PortComboBox.SelectedItem,
						BaudRate = (int)BaudRateNumberBox.Value,
						DtrEnable = true,
						ReadTimeout = 500
					};
					// choices for noise reduction
					switch (NoiseComboBox.SelectedIndex)
					{
						case 0:
							connection.JumpDistance = 6;
							break;
						case 1:
							connection.JumpDistance = 12;
							break;
						case 2:
							connection.JumpDistance = 24;
							break;
					}
				}
				// send error if baud rate is negative
				catch (ArgumentOutOfRangeException)
				{
					NewConnectionButton.IsEnabled = true;
					errorBar.IsOpen = true;
					errorBar.Message = "Baud rate should be a positive integer.";
					return;
				}
				MakeConnection();
			}
		}
		public void MakeConnection()
		{
			// try to connect
			bool isConnected = connection.Start();
			// if successful then add sliders to the window
			if (isConnected == true)
			{
				NoConnectionText.Visibility = Visibility.Collapsed;
				DisconnectButton.IsEnabled = true;
				foreach (Slider slider in connection.Sliders)
				{
					// copy from connection to window
					Sliders.Add(slider);
				}
			}
			// otherwise send error message
			else
			{
				errorBar.IsOpen = true;
				NewConnectionButton.IsEnabled = true;
				errorBar.Message = "Unable to communicate with device.";
				connection.Close();
			}
		}
		// refreshed all port names
		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			SerialPort defaultSerial = new SerialPort();
			PortComboBox.ItemsSource = SerialPort.GetPortNames();
			PortComboBox.SelectedItem = defaultSerial.PortName;
		}
		// when window closes
		private void Window_Closed(object sender, WindowEventArgs args)
		{

			if (ShuttingDown)
			{
				TrayIcon.Dispose();
				if (connection.IsRunning)
				{
					AppStorage.Save(connection);
				}
				connection.Stop(); // make sure the connection is disconnected properly
				connection.Dispose();
			}
			else
			{
				args.Handled = true;
				this.Hide();
				SendNotificationToast("App is running in the background", "For options right click the tray icon");
			}
			
		}
		// toast notification
		public static bool SendNotificationToast(string title, string message)
		{
			var toast = new AppNotificationBuilder()
				.AddText(title)
				.AddText(message)
				.BuildNotification();

			AppNotificationManager.Default.Show(toast);
			return toast.Id != 0;

		}
		// button that disconnects from the serial connection
		private void DisconnectButton_Click(object sender, RoutedEventArgs e)
		{
			connection.Stop();
            StorageFolder defaultPath = ApplicationData.Current.LocalFolder;
            if (File.Exists(Path.Join(defaultPath.Path, "current.json"))) { AppStorage.Delete(); }
			
			NoConnectionText.Visibility = Visibility.Visible;
			Sliders.Clear(); 
			NewConnectionButton.IsEnabled = true;
			DisconnectButton.IsEnabled = false;
		}

		[RelayCommand]
		public void ShowHideWindow()
		{
			if (this.Visible)
			{
				this.Hide();
			}
			else
			{
				this.Show();
			}
		}
		private void CloseApp(object sender, RoutedEventArgs e)
		{
			ShuttingDown = true;
			this.Close();
		}

		private void OpenDocs_Button(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo("") { UseShellExecute = true });
		}

		private void MisclickLink_Hyperlink(object sender, RoutedEventArgs e)
		{
			Process.Start(new ProcessStartInfo("") { UseShellExecute = true });
		}
		private async void rootPanel_Loaded(object sender, RoutedEventArgs e)
		{
			Debug.WriteLine("loading...");
			//loading existing connection
			StorageFolder defaultPath = ApplicationData.Current.LocalFolder;
			if (File.Exists(Path.Join(defaultPath.Path, "current.json")))
			{
				try
				{
					NewConnectionButton.IsEnabled = false;
					connection = await AppStorage.Load();
					MakeConnection();
				}
				catch (JsonException)
				{
                    NewConnectionButton.IsEnabled = true;
				}

			}
		}
	}
	
	
	

}
