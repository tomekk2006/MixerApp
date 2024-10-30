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


namespace MixerApp
{
    [ObservableObject]
    public sealed partial class MainWindow : Window
    {
        ObservableCollection<Slider> Sliders { get; set; }
        Connection connection { get; set; }
        

        public MainWindow()
        {
            this.InitializeComponent();

            // Set up for New Connection Dialog
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
            if (result == ContentDialogResult.Primary) { 
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
             TrayIcon.Dispose();
                connection.Stop(); // make sure the connection is disconnected properly
        }
        // button that disconnects from the serial connection
        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            connection.Stop();
            NoConnectionText.Visibility = Visibility.Visible;
            Sliders.Clear();
            NewConnectionButton.IsEnabled = true;
            DisconnectButton.IsEnabled = false;
        }

        private void ShowHideWindow(object sender, RoutedEventArgs e)
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
        [RelayCommand]
        public void ShowHideWindow2()
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
           
            this.Close();
        }

        private void OpenDocs_Button(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://mixer.misclick.cc") { UseShellExecute = true });
        }

        private void MisclickLink_Hyperlink(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("https://misclick.cc") { UseShellExecute = true });
        }
        
    }
    public class Connection : SerialPort
    {
        public bool IsRunning { get; set; }
        public ObservableCollection<Slider> Sliders { set; get; }
        private DispatcherQueue dispatcher;
        private Thread readThread;
        public int JumpDistance { get; set; }
        public Connection()
        {
            IsRunning = false;
            dispatcher = DispatcherQueue.GetForCurrentThread();

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
                    catch (InvalidOperationException) {
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
    public class AudioControl
    {
        public enum Type
        {
            MICROPHONE,
            SPEAKER,
            SYSTEM,
            PROCESS,
            UNMAPPED
        }
        public bool AllowSearch { get; set; }
        public string DeviceId { get; set; }
        public Type SliderType { get; set; }
        public bool Editable { get; set; }
        public Slider Parent { get; set; }
        public string Placeholder { get; set; }
        public string Glyph { get; set; }
        public string Mapping {  get; set; }
        public AudioControl(Type type, Slider parent)
        {
            Parent = parent;
            AllowSearch = true;
            Mapping = "";
            switch (type) { 
                case Type.MICROPHONE:
                    SliderType = Type.MICROPHONE;
                    Glyph = "\uE720";
                    Placeholder = "Input Device (default if left empty)";
                    Editable = true;
                    break;
                case Type.SPEAKER:
                    SliderType = Type.SPEAKER;                    
                    Glyph = "\uE7F5";
                    Placeholder = "Output Device (default if left empty)";
                    Editable = true;
                    break;
                case Type.SYSTEM:
                    SliderType = Type.SYSTEM;                    
                    Glyph = "\uE770";
                    Placeholder = "System Sounds";
                    Editable = false; 
                    break;
                case Type.PROCESS: 
                    SliderType = Type.PROCESS;
                    Glyph = "\uECAA";
                    Placeholder = "Process Name";
                    Editable = true;
                    break;
                case Type.UNMAPPED:
                    SliderType = Type.UNMAPPED;
                    Glyph = "\uE74C";
                    Placeholder = "Unmapped Processes";
                    Editable = false;
                    break;

            }
        }
        // Delete itself from parent collection
        public void DeleteObject(object sender, RoutedEventArgs e)
        {
            Parent.AudioControls.Remove(this);
            Parent.OnPropertyChanged("ControlsText");
        }
        // Updates Mapping
        public void TextBox_KeyUp(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            DeviceId = null;
            AllowSearch = true;

            string Text = textBox.Text;
            Mapping = Text;
        }
    }
}
