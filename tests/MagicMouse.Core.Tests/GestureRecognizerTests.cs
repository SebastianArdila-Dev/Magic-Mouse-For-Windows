using MagicMouse.Core.Gestures;
using MagicMouse.Core.Settings;
using MagicMouse.Core.Touch;
using Xunit;

namespace MagicMouse.Core.Tests;

public class GestureRecognizerTests
{
    [Fact]
    public void Recognizes_one_finger_swipe_left()
    {
        var recognizer = new GestureRecognizer();
        var start = DateTimeOffset.UtcNow;
        recognizer.Process(Frame(start, (1, .82, .5)));
        recognizer.Process(Frame(start.AddMilliseconds(90), (1, .52, .5)));
        var result = recognizer.Process(Frame(start.AddMilliseconds(110)));

        Assert.Equal(GestureKind.OneFingerSwipeLeft, result?.Kind);
    }

    [Fact]
    public void Maps_recognized_gesture_to_saved_action()
    {
        var settings = new UserSettings();
        settings.Choices["gesture:Tap con 2 dedos"] = "Clic secundario";
        var gesture = new GestureEvent(GestureKind.TwoFingerTap, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(70), 0);

        Assert.Equal("Clic secundario", GestureActionMapper.Map(gesture, settings));
    }

    [Fact]
    public void Recognizes_two_finger_swipe_down()
    {
        var recognizer = new GestureRecognizer();
        var start = DateTimeOffset.UtcNow;
        recognizer.Process(Frame(start, (1, .4, .25), (2, .6, .25)));
        recognizer.Process(Frame(start.AddMilliseconds(120), (1, .4, .58), (2, .6, .58)));

        Assert.Equal(GestureKind.TwoFingerSwipeDown, recognizer.Process(Frame(start.AddMilliseconds(150)))?.Kind);
    }

    [Fact]
    public void Recognizes_one_finger_tap()
    {
        var recognizer = new GestureRecognizer();
        var start = DateTimeOffset.UtcNow;
        recognizer.Process(Frame(start, (1, .5, .5)));

        Assert.Equal(GestureKind.OneFingerTap, recognizer.Process(Frame(start.AddMilliseconds(80)))?.Kind);
    }

    [Fact]
    public void Cancels_gesture_when_contact_identity_changes()
    {
        var recognizer = new GestureRecognizer();
        var start = DateTimeOffset.UtcNow;
        recognizer.Process(Frame(start, (1, .2, .5)));
        recognizer.Process(Frame(start.AddMilliseconds(60), (2, .8, .5)));

        Assert.Null(recognizer.Process(Frame(start.AddMilliseconds(90))));
    }

    private static TouchFrame Frame(DateTimeOffset at, params (int Id, double X, double Y)[] contacts) =>
        new(at, contacts.Select(contact => new TouchContact(contact.Id, contact.X, contact.Y)).ToArray());
}
