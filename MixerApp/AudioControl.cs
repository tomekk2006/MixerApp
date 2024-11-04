using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixerApp
{
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
		public string Mapping { get; set; }
		public AudioControl(Type type, Slider parent)
		{
			Parent = parent;
			AllowSearch = true;
			Mapping = "";
			switch (type)
			{
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
