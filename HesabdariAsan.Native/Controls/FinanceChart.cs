using System.Windows;
using System.Windows.Media;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Controls;

public sealed class FinanceChart : FrameworkElement
{
    public static readonly DependencyProperty DataProperty=DependencyProperty.Register(nameof(Data),typeof(IEnumerable<DailyFinancePoint>),typeof(FinanceChart),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
    public IEnumerable<DailyFinancePoint>? Data{get=> (IEnumerable<DailyFinancePoint>?)GetValue(DataProperty);set=>SetValue(DataProperty,value);}
    public bool ShowAllSeries{get;set;}=true;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var data=Data?.ToList()??new List<DailyFinancePoint>();if(ActualWidth<80||ActualHeight<80)return;
        var muted=Application.Current?.Resources["Muted"] as Brush??Brushes.Gray;var border=Application.Current?.Resources["Border"] as Brush??Brushes.LightGray;
        var left=54d;var right=14d;var top=18d;var bottom=34d;var w=Math.Max(1,ActualWidth-left-right);var h=Math.Max(1,ActualHeight-top-bottom);
        for(var i=0;i<=4;i++){var y=top+h*i/4;dc.DrawLine(new Pen(border,1),new Point(left,y),new Point(left+w,y));}
        if(data.Count==0){DrawText(dc,"داده‌ای برای نمودار وجود ندارد",new Point(left+w/2-75,top+h/2),muted,13);return;}
        var max=data.SelectMany(p=>ShowAllSeries?new double[]{p.Sales,p.Expenses,p.Debt,p.Payments,Math.Abs(p.NetProfit)}:new double[]{p.Sales,p.Expenses,Math.Abs(p.NetProfit)}).DefaultIfEmpty(0).Max();if(max<=0)max=1;
        DrawSeries(dc,data,p=>p.Sales,Brushes.DodgerBlue,left,top,w,h,max);
        DrawSeries(dc,data,p=>p.Expenses,Brushes.OrangeRed,left,top,w,h,max);
        if(ShowAllSeries){DrawSeries(dc,data,p=>p.Debt,Brushes.MediumPurple,left,top,w,h,max);DrawSeries(dc,data,p=>p.Payments,Brushes.DeepSkyBlue,left,top,w,h,max);}
        DrawSeries(dc,data,p=>p.NetProfit,Brushes.MediumSeaGreen,left,top,w,h,max);
        var labelCount=Math.Min(6,data.Count);for(var j=0;j<labelCount;j++){var idx=labelCount==1?0:(int)Math.Round(j*(data.Count-1d)/(labelCount-1));var x=left+(data.Count==1?w/2:idx*w/(data.Count-1));DrawText(dc,data[idx].Date.ToString("MM/dd"),new Point(x-16,top+h+8),muted,11);}
    }
    private static void DrawSeries(DrawingContext dc,List<DailyFinancePoint> data,Func<DailyFinancePoint,long> value,Brush brush,double left,double top,double w,double h,double max)
    {
        if(data.Count<2)return;var geo=new StreamGeometry();using(var ctx=geo.Open()){for(var i=0;i<data.Count;i++){var x=left+i*w/(data.Count-1);var v=value(data[i]);var y=top+h-(Math.Max(0,v)/max*h);if(i==0)ctx.BeginFigure(new Point(x,y),false,false);else ctx.LineTo(new Point(x,y),true,false);}}geo.Freeze();dc.DrawGeometry(null,new Pen(brush,2.2),geo);
    }
    private static void DrawText(DrawingContext dc,string text,Point p,Brush brush,double size)
    {
        var ft=new FormattedText(text,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,brush,VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);dc.DrawText(ft,p);
    }
}
