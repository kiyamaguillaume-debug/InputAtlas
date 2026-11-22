using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace InputAtlas {
public static class Theme {
 public static bool Dark,Chinese;
 public static Brush Get(string key){return Application.Current!=null&&Application.Current.Resources.Contains(key)?(Brush)Application.Current.Resources[key]:Brushes.SlateGray;}
 public static Brush Text {get{return Get("Ink");}}
 public static Brush Muted {get{return Get("Muted");}}
 public static Brush Card {get{return Get("Card");}}
 public static Color Empty {get{return ((SolidColorBrush)Get("Empty")).Color;}}
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 public static void Apply(Window window,bool dark){
  Dark=dark;var r=Application.Current.Resources;
  string[] keys={"Canvas","Panel","Card","Field","Ink","Muted","Stroke","Accent","AccentSoft","Hover","GridLine","Empty","RowAlternate","SelectedInk","SwitchTrack","SwitchOutline","SwitchKnob","TabSurface","TabStroke","TabSelectedStroke"};
  string[] light={"#F4F6FB","#FFFFFF","#FFFFFF","#F6F8FC","#18273E","#61728C","#E2E8F1","#4368E8","#EAF0FF","#EDF2FC","#E8EDF5","#E5EAF2","#F7F9FC","#2449BD","#D6D6D6","#BBBBBB","#FFFFFF","#F1F3F6","#D4D9E2","#B4C3F2"};
  string[] night={"#121212","#191919","#222222","#292929","#F0F0F0","#B0B0B0","#3C3C3C","#D0D0D0","#383838","#303030","#3A3A3A","#343434","#272727","#FFFFFF","#484848","#6B6B6B","#D3D3D3","#1C1C1C","#454545","#777777"};
  for(int i=0;i<keys.Length;i++){var b=new SolidColorBrush(Palette.Parse((dark?night:light)[i]));b.Freeze();r[keys[i]]=b;}
  if(!r.Contains("NativeStylesLoaded")){r.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles));r["NativeStylesLoaded"]=true;}
  window.SetResourceReference(Window.BackgroundProperty,"Canvas");window.SetResourceReference(Window.ForegroundProperty,"Ink");
  try{int v=dark?1:0;IntPtr h=new WindowInteropHelper(window).Handle;if(h!=IntPtr.Zero)DwmSetWindowAttribute(h,20,ref v,4);}catch{}
 }
 public static Color Blend(Color end,double fraction){Color start=((SolidColorBrush)Card).Color;return Color.FromRgb((byte)(start.R+(end.R-start.R)*fraction),(byte)(start.G+(end.G-start.G)*fraction),(byte)(start.B+(end.B-start.B)*fraction));}
 const string Styles=@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
 <Style TargetType='TextBlock'><Setter Property='Foreground' Value='{DynamicResource Ink}'/></Style>
 <Style TargetType='Button'>
  <Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/>
  <Setter Property='Padding' Value='14,10'/><Setter Property='Cursor' Value='Hand'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
   <Border x:Name='Surface' CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}'>
    <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
   </Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Background' Value='{DynamicResource AccentSoft}'/></Trigger></ControlTemplate.Triggers>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Cursor' Value='Hand'/></Style>
 <Style x:Key='ThemeSwitch' TargetType='CheckBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='CheckBox'><DockPanel><Grid Width='38' Height='22' Margin='0,0,10,0'><Border x:Name='Track' CornerRadius='11' Background='{DynamicResource SwitchTrack}' BorderBrush='{DynamicResource SwitchOutline}' BorderThickness='1'/><Ellipse x:Name='Dot' Width='14' Height='14' Fill='{DynamicResource SwitchKnob}' HorizontalAlignment='Left' Margin='4,0,0,0'/></Grid><ContentPresenter VerticalAlignment='Center'/></DockPanel><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Dot' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='Dot' Property='Margin' Value='0,0,4,0'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='TextBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/><Setter Property='CaretBrush' Value='{DynamicResource Ink}'/><Setter Property='SelectionBrush' Value='{DynamicResource AccentSoft}'/><Setter Property='Padding' Value='9,7'/></Style>
 <Style x:Key='ComboToggle' TargetType='ToggleButton'><Setter Property='Focusable' Value='False'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToggleButton'><Border Background='Transparent'/></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='ComboBox'>
  <Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/><Setter Property='MinHeight' Value='38'/><Setter Property='Padding' Value='11,8'/><Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'>
   <Grid><Border CornerRadius='8' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'/>
    <ToggleButton Style='{StaticResource ComboToggle}' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'/>
    <ContentPresenter Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' Margin='11,8,28,8' VerticalAlignment='Center' IsHitTestVisible='False'/>
    <TextBlock Text='⌄' Foreground='{DynamicResource Muted}' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,12,4' IsHitTestVisible='False'/>
    <Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='None'>
     <Border MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' CornerRadius='8' Background='{DynamicResource Panel}' BorderBrush='{DynamicResource Stroke}' BorderThickness='1' Padding='4'>
      <ScrollViewer MaxHeight='300'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
     </Border>
    </Popup>
   </Grid>
  </ControlTemplate></Setter.Value></Setter>
 </Style>
 <Style TargetType='ComboBoxItem'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='10,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='Item' CornerRadius='5' Padding='{TemplateBinding Padding}' Background='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Item' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='Item' Property='Background' Value='{DynamicResource AccentSoft}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='DatePicker'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/><Setter Property='Padding' Value='3'/><Setter Property='SelectedDateFormat' Value='Short'/></Style>
 <Style TargetType='DatePickerTextBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='6'/></Style>
 <Style TargetType='Calendar'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Card}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/></Style>
 <Style TargetType='TabControl'><Setter Property='OverridesDefaultStyle' Value='True'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabControl'><WrapPanel IsItemsHost='True' Background='Transparent'/></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='TabItem'><Setter Property='OverridesDefaultStyle' Value='True'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Foreground' Value='{DynamicResource Muted}'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Margin' Value='0,0,7,3'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'><Border x:Name='Pill' Background='{DynamicResource TabSurface}' BorderBrush='{DynamicResource TabStroke}' BorderThickness='1' CornerRadius='9' Padding='15,10'><ContentPresenter x:Name='HeaderContent' ContentSource='Header' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='Pill' Property='Background' Value='{DynamicResource AccentSoft}'/><Setter TargetName='Pill' Property='BorderBrush' Value='{DynamicResource TabSelectedStroke}'/><Setter Property='Foreground' Value='{DynamicResource SelectedInk}'/><Setter Property='FontWeight' Value='SemiBold'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Pill' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='Pill' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType='DataGrid'><Setter Property='Background' Value='{DynamicResource Card}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/><Setter Property='RowBackground' Value='{DynamicResource Card}'/><Setter Property='AlternatingRowBackground' Value='{DynamicResource RowAlternate}'/><Setter Property='HorizontalGridLinesBrush' Value='{DynamicResource Stroke}'/><Setter Property='ColumnHeaderHeight' Value='36'/><Setter Property='RowHeight' Value='33'/><Setter Property='HeadersVisibility' Value='Column'/></Style>
 <Style TargetType='DataGridColumnHeader'><Setter Property='Background' Value='{DynamicResource Field}'/><Setter Property='Foreground' Value='{DynamicResource Muted}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/><Setter Property='Padding' Value='10,7'/><Setter Property='FontWeight' Value='SemiBold'/></Style>
 <Style TargetType='DataGridCell'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderThickness' Value='0'/><Setter Property='Padding' Value='8,4'/><Style.Triggers><Trigger Property='IsSelected' Value='True'><Setter Property='Background' Value='{DynamicResource AccentSoft}'/></Trigger></Style.Triggers></Style>
 <Style TargetType='ToolTip'><Setter Property='Background' Value='{DynamicResource Panel}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='BorderBrush' Value='{DynamicResource Stroke}'/><Setter Property='Padding' Value='12,9'/></Style>
 <Style x:Key='ScrollThumb' TargetType='Thumb'><Setter Property='Background' Value='{DynamicResource Stroke}'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Thumb'><Border Background='{TemplateBinding Background}' CornerRadius='5' Margin='2'/></ControlTemplate></Setter.Value></Setter><Style.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='{DynamicResource Muted}'/></Trigger></Style.Triggers></Style>
 <Style TargetType='ScrollBar'><Setter Property='Width' Value='11'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' Orientation='Vertical' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb Style='{StaticResource ScrollThumb}'/></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>";
}
}
