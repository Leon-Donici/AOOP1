using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace AvaloniaExercisesSolution;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        this.Width = 600; 
        this.Height = 500;
    }
    private void Exercise2ShowOutput_Click(object sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("Exercise2TextBox");
        var comboBox = this.FindControl<ComboBox>("Exercise2ComboBox");
        var checkBox = this.FindControl<CheckBox>("Exercise2CheckBox");
        var outputTextBlock = this.FindControl<TextBlock>("OutputTextBlock");

        string output = $"TextBox: {textBox.Text}, ComboBox: {comboBox.SelectionBoxItem}, CheckBox: {checkBox.IsChecked}";
        outputTextBlock.Text = output;
    }

    private void Exercise3ShowImage_Click(object sender, RoutedEventArgs e)
    {
        var catRadioButton = this.FindControl<RadioButton>("CatRadioButton");
        var dogRadioButton = this.FindControl<RadioButton>("DogRadioButton");
        var birdRadioButton = this.FindControl<RadioButton>("BirdRadioButton");
        var animalImage = this.FindControl<Image>("AnimalImage");

        if (catRadioButton.IsChecked == true)
        {
            
          animalImage.Source =  new Bitmap(AssetLoader.Open(new Uri("avares://AvaloniaExercisesSolution/Assets/cat.jpg")));

        }
        else if (dogRadioButton.IsChecked == true)
        {
            animalImage.Source =  new Bitmap(AssetLoader.Open(new Uri("avares://AvaloniaExercisesSolution/Assets/dog.jpg")));

        }
        else if (birdRadioButton.IsChecked == true)
        {
            animalImage.Source =  new Bitmap(AssetLoader.Open(new Uri("avares://AvaloniaExercisesSolution/Assets/bird.jpg")));

        }
    }
    private void Exercise4_Click(object sender, RoutedEventArgs e)
    {
        double result = 0;
        if (double.TryParse(Number1TextBox.Text, out double num1) &&
            double.TryParse(Number2TextBox.Text, out double num2))
        {
            if (sender.Equals(Add))
            {
                result = num1 + num2;
            }
            
            if (sender.Equals(Subtract))
            {
                result = num1 - num2;
            }
            
            if (sender.Equals(Multiply))
            {
                result = num1 * num2;
            }

            ResultTextBox.Text = result.ToString();
        }
        else
        {
            ResultTextBox.Text = "Invalid input";
        }
    }
    
}

