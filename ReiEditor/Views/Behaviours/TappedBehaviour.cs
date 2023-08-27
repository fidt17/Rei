using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ReiEditor.Views.Behaviours;

public class TappedBehaviour : AvaloniaObject
{
    static TappedBehaviour()
    {
        CommandProperty.Changed.Subscribe(x => HandleCommandChanged(x.Sender, x.NewValue.GetValueOrDefault<ICommand>()!));
    }

    public static readonly AttachedProperty<ICommand> CommandProperty = AvaloniaProperty.RegisterAttached<TappedBehaviour, Interactive, ICommand>("Command", default!, false, BindingMode.OneTime);
    public static readonly AttachedProperty<object> CommandParameterProperty = AvaloniaProperty.RegisterAttached<TappedBehaviour, Interactive, object>("CommandParameter");

    private static void HandleCommandChanged(AvaloniaObject element, ICommand commandValue)
    {
        if (element is Interactive interactElem)
        {
            if (commandValue != null)
            {
                // Add non-null value
                interactElem.AddHandler(InputElement.TappedEvent, Handler!);
            }
            else
            {
                // remove prev value
                interactElem.RemoveHandler(InputElement.TappedEvent, Handler!);
            }
        }

        // local handler fcn
        static void Handler(object s, RoutedEventArgs e)
        {
            if (s is Interactive interactElem)
            {
                // This is how we get the parameter off of the gui element.
                object commandParameter = interactElem.GetValue(CommandParameterProperty);
                ICommand commandValue = interactElem.GetValue(CommandProperty);
                if (commandValue?.CanExecute(commandParameter) == true)
                {
                    commandValue.Execute(commandParameter);
                }
            }
        }
    }

    public static void SetCommand(AvaloniaObject element, ICommand commandValue) => element.SetValue(CommandProperty, commandValue);

    public static ICommand GetCommand(AvaloniaObject element) => element.GetValue(CommandProperty);
}