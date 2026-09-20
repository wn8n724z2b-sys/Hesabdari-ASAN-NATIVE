using System.Windows;
using System.Windows.Media;
using HesabdariAsan.Native.Models;

namespace HesabdariAsan.Native.Controls;

public sealed class FinanceChart : FrameworkElement
{
    public static readonly DependencyProperty DataProperty = DependencyProperty.Register(
        nameof(Data), typeof(IEnumerable<DailyFinancePoint>), typeof(FinanceChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowAllSeriesProperty = DependencyProperty.Register(
        nameof(ShowAllSeries), typeof(bool), typeof(FinanceChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowDashboardSeriesProperty = DependencyProperty.Register(
        nameof(ShowDashboardSeries), typeof(bool), typeof(FinanceChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowSalesProperty = DependencyProperty.Register(
        nameof(ShowSales), typeof(bool), typeof(FinanceChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowExpensesProperty = DependencyProperty.Register(
        nameof(ShowExpenses), typeof(bool), typeof(FinanceChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowDebtProperty = DependencyProperty.Register(
        nameof(ShowDebt), typeof(bool), typeof(FinanceChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowPaymentsProperty = DependencyProperty.Register(
        nameof(ShowPayments), typeof(bool), typeof(FinanceChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowProfitProperty = DependencyProperty.Register(
        nameof(ShowProfit), typeof(bool), typeof(FinanceChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<DailyFinancePoint>? Data { get => (IEnumerable<DailyFinancePoint>?)GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public bool ShowAllSeries { get => (bool)GetValue(ShowAllSeriesProperty); set => SetValue(ShowAllSeriesProperty, value); }
    public bool ShowDashboardSeries { get => (bool)GetValue(ShowDashboardSeriesProperty); set => SetValue(ShowDashboardSeriesProperty, value); }
    public bool ShowSales { get => (bool)GetValue(ShowSalesProperty); set => SetValue(ShowSalesProperty, value); }
    public bool ShowExpenses { get => (bool)GetValue(ShowExpensesProperty); set => SetValue(ShowExpensesProperty, value); }
    public bool ShowDebt { get => (bool)GetValue(ShowDebtProperty); set => SetValue(ShowDebtProperty, value); }
    public bool ShowPayments { get => (bool)GetValue(ShowPaymentsProperty); set => SetValue(ShowPaymentsProperty, value); }
    public bool ShowProfit { get => (bool)GetValue(ShowProfitProperty); set => SetValue(ShowProfitProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var data = Data?.ToList() ?? new List<DailyFinancePoint>();
        if (ActualWidth < 80 || ActualHeight < 80) return;

        var muted = ResourceBrush("Muted", Brushes.Gray);
        var border = ResourceBrush("Border", Brushes.LightGray);
        var accent = ResourceBrush("Accent", Brushes.DodgerBlue);
        var danger = ResourceBrush("Danger", Brushes.IndianRed);
        var success = ResourceBrush("Success", Brushes.MediumSeaGreen);
        var purple = ResourceBrush("Purple", Brushes.MediumPurple);
        var cyan = ResourceBrush("Cyan", Brushes.DeepSkyBlue);

        var left = 54d; var right = 14d; var top = 18d; var bottom = 34d;
        var w = Math.Max(1, ActualWidth - left - right); var h = Math.Max(1, ActualHeight - top - bottom);
        for (var i = 0; i <= 4; i++)
        {
            var y = top + h * i / 4;
            dc.DrawLine(new Pen(border, 1), new Point(left, y), new Point(left + w, y));
        }
        if (data.Count == 0)
        {
            DrawText(dc, "داده‌ای برای نمودار وجود ندارد", new Point(left + w / 2 - 75, top + h / 2), muted, 13);
            return;
        }

        var values = new List<double>();
        if (ShowDashboardSeries)
        {
            values.AddRange(data.SelectMany(p => new double[] { p.Sales, p.Debt, p.NetProfit }));
        }
        else
        {
            if (ShowSales) values.AddRange(data.Select(p => (double)p.Sales));
            if (ShowExpenses) values.AddRange(data.Select(p => (double)p.Expenses));
            if (ShowAllSeries && ShowDebt) values.AddRange(data.Select(p => (double)p.Debt));
            if (ShowAllSeries && ShowPayments) values.AddRange(data.Select(p => (double)p.Payments));
            if (ShowProfit) values.AddRange(data.Select(p => (double)p.NetProfit));
        }
        var minValue = Math.Min(0, values.DefaultIfEmpty(0).Min());
        var maxValue = Math.Max(0, values.DefaultIfEmpty(0).Max());
        if (Math.Abs(maxValue - minValue) < 0.001) maxValue = minValue + 1;

        // Match the WebView chart behavior: when profit goes below zero, keep a visible zero axis.
        if (minValue < 0 && maxValue > 0)
        {
            var zeroY = ValueToY(0, minValue, maxValue, top, h);
            var zeroPen = new Pen(muted, 1) { DashStyle = new DashStyle(new double[] { 4, 5 }, 0) };
            zeroPen.Freeze();
            dc.DrawLine(zeroPen, new Point(left, zeroY), new Point(left + w, zeroY));
        }

        if (ShowDashboardSeries)
        {
            DrawSeries(dc, data, p => p.Sales, accent, left, top, w, h, minValue, maxValue);
            DrawSeries(dc, data, p => p.Debt, danger, left, top, w, h, minValue, maxValue);
            DrawSeries(dc, data, p => p.NetProfit, success, left, top, w, h, minValue, maxValue);
        }
        else
        {
            if (ShowSales) DrawSeries(dc, data, p => p.Sales, accent, left, top, w, h, minValue, maxValue);
            if (ShowExpenses) DrawSeries(dc, data, p => p.Expenses, danger, left, top, w, h, minValue, maxValue);
            if (ShowAllSeries && ShowDebt) DrawSeries(dc, data, p => p.Debt, purple, left, top, w, h, minValue, maxValue);
            if (ShowAllSeries && ShowPayments) DrawSeries(dc, data, p => p.Payments, cyan, left, top, w, h, minValue, maxValue);
            if (ShowProfit) DrawSeries(dc, data, p => p.NetProfit, success, left, top, w, h, minValue, maxValue);
        }

        var labelCount = Math.Min(6, data.Count);
        for (var j = 0; j < labelCount; j++)
        {
            var idx = labelCount == 1 ? 0 : (int)Math.Round(j * (data.Count - 1d) / (labelCount - 1));
            var x = left + (data.Count == 1 ? w / 2 : idx * w / (data.Count - 1));
            DrawText(dc, data[idx].Date.ToString("MM/dd"), new Point(x - 16, top + h + 8), muted, 11);
        }
    }

    private static Brush ResourceBrush(string key, Brush fallback) => Application.Current?.Resources[key] as Brush ?? fallback;

    private static void DrawSeries(DrawingContext dc, List<DailyFinancePoint> data, Func<DailyFinancePoint, long> value, Brush brush, double left, double top, double w, double h, double min, double max)
    {
        if (data.Count == 1)
        {
            var y = ValueToY(value(data[0]), min, max, top, h);
            dc.DrawEllipse(brush, null, new Point(left + w / 2, y), 2.7, 2.7);
            return;
        }
        if (data.Count < 2) return;
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < data.Count; i++)
            {
                var x = left + i * w / (data.Count - 1);
                var y = ValueToY(value(data[i]), min, max, top, h);
                if (i == 0) ctx.BeginFigure(new Point(x, y), false, false); else ctx.LineTo(new Point(x, y), true, false);
            }
        }
        geo.Freeze();
        var pen = new Pen(brush, 2.15) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static double ValueToY(double value, double min, double max, double top, double h)
    {
        var range = Math.Max(0.001, max - min);
        var ratio = (value - min) / range;
        return top + h - ratio * h;
    }

    private static void DrawText(DrawingContext dc, string text, Point p, Brush brush, double size)
    {
        var dpi = Application.Current?.MainWindow is null ? 1.0 : VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip;
        var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Nirmala UI"), size, brush, dpi);
        dc.DrawText(ft, p);
    }
}
