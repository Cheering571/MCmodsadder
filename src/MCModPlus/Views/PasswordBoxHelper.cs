using System.Windows;
using System.Windows.Controls;

namespace MCModPlus.Views;

public static class PasswordBoxHelper
{
    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.RegisterAttached(
            "Password",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, OnPasswordChanged));

    private static readonly DependencyProperty UpdatingProperty =
        DependencyProperty.RegisterAttached(
            "Updating",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    public static string GetPassword(DependencyObject element) =>
        (string)element.GetValue(PasswordProperty);

    public static void SetPassword(DependencyObject element, string value) =>
        element.SetValue(PasswordProperty, value);

    private static bool GetUpdating(DependencyObject element) =>
        (bool)element.GetValue(UpdatingProperty);

    private static void SetUpdating(DependencyObject element, bool value) =>
        element.SetValue(UpdatingProperty, value);

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox || GetUpdating(passwordBox))
        {
            return;
        }

        passwordBox.PasswordChanged -= PasswordBoxOnPasswordChanged;
        passwordBox.Password = e.NewValue as string ?? string.Empty;
        passwordBox.PasswordChanged += PasswordBoxOnPasswordChanged;
    }

    private static void PasswordBoxOnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        SetUpdating(passwordBox, true);
        SetPassword(passwordBox, passwordBox.Password);
        SetUpdating(passwordBox, false);
    }
}
