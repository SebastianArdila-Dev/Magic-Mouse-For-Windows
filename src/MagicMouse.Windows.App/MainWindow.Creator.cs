using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace MagicMouse.Windows.App;

public sealed partial class MainWindow
{
    private UIElement? _standardModalContent;
    private readonly List<CompositionObject> _creatorMotion = [];
    private Grid? _creatorFluidHost;
    private bool _creatorOpen;
    private Button? _creatorPrimaryButton;
    private async Task ShowCreatorAsync()
    {
        if(_closed) return;
        if(_creatorOpen) { _creatorPrimaryButton?.Focus(FocusState.Programmatic); return; }
        RestoreCreatorContent();
        var completion=ShowSheetAsync("Magic Mouse for Windows","",accept:"Entendido");
        _standardModalContent=ModalCard.Child; _creatorOpen=true;
        ModalCard.Padding=new Thickness(0);
        ModalCard.BorderBrush=new LinearGradientBrush { StartPoint=new(0,0),EndPoint=new(1,1),GradientStops={new GradientStop{Color=ColorFromHex(DesignTokens.IsDark ? "#77BDD4C5":"#EEFFFFFF"),Offset=0},new GradientStop{Color=ColorFromHex(DesignTokens.IsDark ? "#44567966":"#6689AB99"),Offset=.5},new GradientStop{Color=ColorFromHex(DesignTokens.IsDark ? "#66D0E5DA":"#DDFFFFFF"),Offset=1}} };
        var layer=new Grid();
        var fluid=new FluidBackdropGrid { IsHitTestVisible=false };
        fluid.Children.Add(new Image { Source=new Microsoft.UI.Xaml.Media.Imaging.SvgImageSource(new Uri("ms-appx:///Assets/CreatorMist.svg")),Stretch=Stretch.Fill,Opacity=.6 });
        _creatorFluidHost=fluid; layer.Children.Add(fluid);
        var content=new StackPanel { Padding=new Thickness(32),Spacing=22 };
        var heading=new StackPanel { Orientation=Orientation.Horizontal,Spacing=14 };
        heading.Children.Add(new Border { Width=54,Height=54,CornerRadius=new CornerRadius(16),Background=new SolidColorBrush(ColorFromHex(DesignTokens.IsDark ? "#40567764":"#70D8E8DE")),Child=DeviceImage("MagicMouse.png",48,48,"Magic Mouse") });
        var title=new StackPanel { Spacing=3,VerticalAlignment=VerticalAlignment.Center };
        title.Children.Add(Text("Magic Mouse for Windows",20,DesignTokens.PrimaryText,true));
        title.Children.Add(Text("Hecho con intención. Compartido contigo.",12,DesignTokens.SecondaryText)); heading.Children.Add(title); content.Children.Add(heading);
        var author=new StackPanel { Spacing=5 }; author.Children.Add(Text("Sebastian Ardila",26,DesignTokens.PrimaryText,true)); author.Children.Add(Text("Creador · SebastianArdila-Dev",12,DesignTokens.SecondaryText)); content.Children.Add(author);
        var greeting=new StackPanel { Spacing=12 };
        greeting.Children.Add(Text("Hola, gracias por estar aquí.",18,DesignTokens.PrimaryText,true));
        var purpose=Text("Creé esta app con un objetivo: que puedas usar tu Magic Mouse en Windows de forma gratuita y aprovechar al máximo tu dispositivo Apple.",13,DesignTokens.BodyText); purpose.LineHeight=22; greeting.Children.Add(purpose);
        var support=Text("Si te resulta útil, acompáñame en X y GitHub y apoya mis próximos proyectos. Tus ideas, comentarios y recomendaciones me ayudan a seguir creando herramientas para todos.",13,DesignTokens.BodyText); support.LineHeight=22; greeting.Children.Add(support); content.Children.Add(greeting);
        var buttons=new Grid { ColumnSpacing=12 }; buttons.ColumnDefinitions.Add(new()); buttons.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var social=new StackPanel { Spacing=8 };
        social.Children.Add(CreatorSocialButton("X · @SebastianardSWE","https://x.com/SebastianardSWE",false));
        social.Children.Add(CreatorSocialButton("GitHub · SebastianArdila-Dev","https://github.com/SebastianArdila-Dev",true));
        buttons.Children.Add(social);
        var done=ActionButton("Entendido",()=>{CompleteSheet(true);return Task.CompletedTask;}); done.Background=new SolidColorBrush(ColorFromHex("#527F67")); done.Foreground=new SolidColorBrush(Microsoft.UI.Colors.White); Grid.SetColumn(done,1); buttons.Children.Add(done); content.Children.Add(buttons);
        layer.Children.Add(content); ModalCard.Child=layer;
        fluid.SizeChanged+=OnCreatorSizeChanged;
        _creatorPrimaryButton=done;
        RootGrid.RefreshCursorTree(); done.Focus(FocusState.Programmatic);
        await completion;
    }
    private Button CreatorSocialButton(string label,string url,bool github)
    {
        var button=ActionButton(label,async()=>await global::Windows.System.Launcher.LaunchUriAsync(new Uri(url)));
        button.Padding=new Thickness(10,7,10,7);button.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);button.BorderThickness=new Thickness(0);
        var row=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8 };
        var geometry=github ? "M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82a7.65 7.65 0 0 1 4 0c1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8 8 0 0 0 16 8c0-4.42-3.58-8-8-8Z" : "M0 0H3.8L16 16H12.2Z M12.6 0H15.2L2.6 16H0Z";
        var path=(Microsoft.UI.Xaml.Shapes.Path)Microsoft.UI.Xaml.Markup.XamlReader.Load($"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{geometry}'/>");
        path.Fill=new SolidColorBrush(ColorFromHex(DesignTokens.SecondaryText));
        row.Children.Add(new Viewbox { Width=13,Height=13,Child=path,VerticalAlignment=VerticalAlignment.Center });
        row.Children.Add(Text(label,11,DesignTokens.SecondaryText));button.Content=row;Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button,label);return button;
    }
    private void OnCreatorSizeChanged(object sender,SizeChangedEventArgs args)
    {
        if(sender is Grid host && _creatorOpen && ReferenceEquals(host,_creatorFluidHost) && !_closed)
        {
            try { StartCreatorFluid(host); }
            catch(Exception exception) { _=LogAsync($"Creator animation unavailable: {exception}"); StopCreatorMotion(); }
        }
    }
    private void StopCreatorMotion()
    {
        // Invalidate callbacks before detaching the visual. Closing the container
        // also closes its descendants; stop all animations before any disposal.
        var host=_creatorFluidHost; var motion=_creatorMotion.ToArray();
        _creatorFluidHost=null; _creatorMotion.Clear();
        if(host is not null) try { host.SizeChanged-=OnCreatorSizeChanged; } catch(ObjectDisposedException) { }
        foreach(var item in motion.OfType<Visual>())
            try { item.StopAnimation("Offset"); item.StopAnimation("Scale"); } catch(ObjectDisposedException) { }
        if(host is not null)
            try { ElementCompositionPreview.SetElementChildVisual(host,null); } catch(ObjectDisposedException) { }
        foreach(var item in motion.Reverse())
            try { item.Dispose(); } catch(ObjectDisposedException) { }
    }
    private void RestoreCreatorContent()
    {
        if(!_creatorOpen) return;
        _creatorOpen=false; _creatorPrimaryButton=null;
        StopCreatorMotion();
        ModalCard.Child=_standardModalContent; ModalCard.Padding=new Thickness(28);
        ModalCard.BorderBrush=new SolidColorBrush(ColorFromHex(DesignTokens.CardBorder));
    }
    private void StartCreatorFluid(Grid host)
    {
        if(!_creatorOpen || _closed || !ReferenceEquals(host,_creatorFluidHost) || host.ActualWidth<=0 || host.ActualHeight<=0) return;
        if(_creatorMotion.FirstOrDefault() is ContainerVisual existing) { existing.Size=new((float)host.ActualWidth,(float)host.ActualHeight); return; }
        var compositor=ElementCompositionPreview.GetElementVisual(host).Compositor;
        var container=compositor.CreateContainerVisual(); container.Size=new((float)host.ActualWidth,(float)host.ActualHeight); container.Clip=compositor.CreateInsetClip(); _creatorMotion.Add(container);
        for(var index=0;index<3;index++)
        {
            var brush=compositor.CreateRadialGradientBrush(); brush.CenterPoint=new(.5f,.5f); brush.EllipseRadius=new(.5f,.5f);
            brush.ColorStops.Add(compositor.CreateColorGradientStop(0,ColorFromHex(index==1 ? "#306C9A84":"#22648F79")));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(.5f,ColorFromHex("#146CAE94")));
            brush.ColorStops.Add(compositor.CreateColorGradientStop(1,ColorFromHex("#006CAE94")));
            var blob=compositor.CreateSpriteVisual(); blob.Size=new(450,450); blob.Brush=brush;
            var phase=index*2*Math.PI/3; var cx=(float)host.ActualWidth/2-225; var cy=(float)host.ActualHeight/2-225;
            var drift=compositor.CreateVector3KeyFrameAnimation(); drift.Duration=TimeSpan.FromSeconds(26+index*5); drift.IterationBehavior=AnimationIterationBehavior.Forever;
            for(var frame=0;frame<=8;frame++) { var angle=phase+frame*Math.PI/4; drift.InsertKeyFrame(frame/8f,new Vector3(cx+(float)Math.Cos(angle)*150,cy+(float)Math.Sin(angle)*125,0),compositor.CreateLinearEasingFunction()); }
            blob.Offset=new(cx+(float)Math.Cos(phase)*150,cy+(float)Math.Sin(phase)*125,0);
            if(_systemUiSettings.AnimationsEnabled) blob.StartAnimation("Offset",drift);
            container.Children.InsertAtTop(blob); _creatorMotion.Add(blob); _creatorMotion.Add(brush); drift.Dispose();
        }
        ElementCompositionPreview.SetElementChildVisual(host,container);
    }
}
