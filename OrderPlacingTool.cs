//------------------------------------------------------------------------------
// OrderPlacingTool.cs
//------------------------------------------------------------------------------
// Renders a fully-styled “Trade Manager” panel matching your mock-up.
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Chart;
using TradingPlatform.BusinessLayer.Native;
using TradingPlatform.BusinessLayer.Utils;

#nullable disable
namespace OrderPlacingTool
{
    public class OrderPlacingTool : Indicator
    {
        //── USER SETTINGS ───────────────────────────────────────────────────────────
        private int riskInAmount = 100;
        private double rewardMultiplier = 1.0;
        private int xShift = 30;   // panel left offset
        private int yShift = 30;   // panel top offset

        public override IList<SettingItem> Settings
        {
            get
            {
                var s = base.Settings;
                var sep = s.FirstOrDefault()?.SeparatorGroup;
                s.Add(new SettingItemInteger("riskInAmount", riskInAmount)
                {
                    Text = "Risk Amount",
                    SeparatorGroup = sep
                });
                s.Add(new SettingItemDouble("rewardMultiplier", rewardMultiplier)
                {
                    Text = "Reward Multiplier",
                    Increment = 0.1,
                    DecimalPlaces = 1,
                    SeparatorGroup = sep
                });
                s.Add(new SettingItemInteger("xShift", xShift)
                {
                    Text = "X Offset",
                    SeparatorGroup = sep
                });
                s.Add(new SettingItemInteger("yShift", yShift)
                {
                    Text = "Y Offset",
                    SeparatorGroup = sep
                });
                return s;
            }
            set
            {
                SettingItemExtensions.TryGetValue(value, "riskInAmount", out riskInAmount);
                SettingItemExtensions.TryGetValue(value, "rewardMultiplier", out rewardMultiplier);
                SettingItemExtensions.TryGetValue(value, "xShift", out xShift);
                SettingItemExtensions.TryGetValue(value, "yShift", out yShift);
                base.Settings = value;
                BuildBrushesAndPens();
            }
        }

        //── LAYOUT CONSTANTS ─────────────────────────────────────────────────────────
        const int panelW = 320;
        const int headerH = 36;
        const int row1H = 56;
        const int row2H = 36;
        const int row3H = 36;
        const int row4H = 44;
        const int btnRadius = 6;
        const int gutter = 8;
        const int radioSize = 14;

        //── COLORS & FONTS ───────────────────────────────────────────────────────────
        readonly Color panelBack = Color.FromArgb(30, 30, 40);
        readonly Color headerBack = Color.FromArgb(20, 20, 30);
        readonly Color borderCol = Color.Gray;
        readonly PairColor sellCol = new PairColor { Color1 = Color.Red, Color2 = Color.DarkRed };
        readonly PairColor buyCol = new PairColor { Color1 = Color.Green, Color2 = Color.DarkGreen };
        readonly PairColor beCol = new PairColor
        {
            Color1 = Color.FromArgb(216, 108, 0),    // darker “back”
            Color2 = Color.FromArgb(196, 100, 0)     // darker “border”
        };
        readonly PairColor partCol = new PairColor { Color1 = Color.Green, Color2 = Color.DarkGreen };
        readonly PairColor smallCol = new PairColor { Color1 = Color.FromArgb(50, 50, 60), Color2 = Color.Gray };

        readonly Font titleFont = new Font("Segoe UI", 14, FontStyle.Bold);
        readonly Font mainFont = new Font("Segoe UI", 12, FontStyle.Regular);
        readonly Font smallFont = new Font("Segoe UI", 10, FontStyle.Regular);
        readonly Brush textBrush = Brushes.White;

        // static so nested Button can reference
        private static readonly StringFormat CenterFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        private static readonly StringFormat LeftFormat = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        //── RUNTIME STATE ────────────────────────────────────────────────────────────
        Brush sellBack, buyBack, beBack, partBack, smallBack;
        Pen sellPen, buyPen, bePen, partPen, smallPen;

        Button sellBtn, buyBtn;
        Button[] lotRadios;
        Rectangle cashBox;
        Button beBtn, partBtn;
        Button btnAll, btnProfit, btnLoss, btnStop;
        Rectangle labelCloseTrades, labelCloseOrders;

        int quantity = 100;
        double pipL = 2936.0, pipR = 2795.0;
        double cashAmt = 20.0, beVal = 0.0;

        enum LotMode { None, Cash, RiskBal, RiskEq }
        LotMode lotMode = LotMode.Cash;

        //──────────────────────────────────────────────────────────────────────────────
        public OrderPlacingTool()
        {
            Name = "Trade Manager";
            Description = "In-chart Trade Manager panel";
            SeparateWindow = false;
            AddLineSeries("dummy", Color.Transparent, 1, LineStyle.Solid);
            BuildBrushesAndPens();
        }

        void BuildBrushesAndPens()
        {
            sellBack = new SolidBrush(sellCol.Color1);
            sellPen = new Pen(sellCol.Color2);
            buyBack = new SolidBrush(buyCol.Color1);
            buyPen = new Pen(buyCol.Color2);
            beBack = new SolidBrush(beCol.Color1);
            bePen = new Pen(beCol.Color2);
            partBack = new SolidBrush(partCol.Color1);
            partPen = new Pen(partCol.Color2);
            smallBack = new SolidBrush(smallCol.Color1);
            smallPen = new Pen(smallCol.Color2);
        }

        protected override void OnInit()
        {
            int X = xShift, Y = yShift;

            // Row1: SELL | qty | BUY
            sellBtn = new Button("SELL",
                X, Y + headerH,
                X + (panelW - gutter) / 2, Y + headerH + row1H,
                sellBack, sellPen, mainFont, textBrush);

            buyBtn = new Button("BUY",
                X + (panelW + gutter) / 2, Y + headerH,
                X + panelW, Y + headerH + row1H,
                buyBack, buyPen, mainFont, textBrush);

            // Row3: Lot-Calc radios + cash input
            int radioX = X + gutter;
            int startLotY = Y + headerH + row1H + row2H + gutter;
            int rowHeight = row3H + gutter;
            int lotCount = 4;  // we have 4 radio buttons

            lotRadios = new[]
            {
    new Button("None",
        radioX, startLotY + 0*rowHeight,
        radioX + radioSize, startLotY + 0*rowHeight + radioSize,
        smallBack, smallPen, smallFont, textBrush,
        circle: true),

    new Button("Cash Amount",
        radioX, startLotY + 1*rowHeight,
        radioX + radioSize, startLotY + 1*rowHeight + radioSize,
        smallBack, smallPen, smallFont, textBrush,
        circle: true),
    new Button(
    "Risk Balance",
    radioX,
    startLotY + 2 * rowHeight,
    radioX + radioSize,
    startLotY + 2 * rowHeight + radioSize,
    smallBack, smallPen, smallFont, textBrush,
    circle: true
),
new Button(
    "Risk Equity",
    radioX,
    startLotY + 3 * rowHeight,
    radioX + radioSize,
    startLotY + 3 * rowHeight + radioSize,
    smallBack, smallPen, smallFont, textBrush,
    circle: true
)
};

            // width of the text‐box
            const int cashBoxWidth = 80;
            // 8px gutter to the right edge, plus say another 4px padding for the “USD” label
            int usdLabelWidth = 40;

            cashBox = new Rectangle(
                // panelX + panelW  gives us the right edge of the panel
                // subtract gutter (8px), the USD label width (40px) and the box width
                X + panelW - gutter - usdLabelWidth - cashBoxWidth,
                // same Y as before
                startLotY + 1 * rowHeight + 4,
                cashBoxWidth,
                row3H - 8
            );

            // Row4: Break-Even & Partial now pushed below the entire Lot-Calc block
            int BY = startLotY + lotCount * rowHeight + gutter;
            // instantiate the two buttons at the new BY
            beBtn = new Button(
                "Move SL To BE",
                 X + gutter, BY,
                 X + 160, BY + row4H,
                 beBack, bePen, smallFont, textBrush
            );

            partBtn = new Button(
                "Close Part",
                 X + 176, BY,
                 X + 296, BY + row4H,
                 partBack, partPen, smallFont, textBrush
            );

            int PY = BY + row4H;

            // place labels immediately below the BE & Partial-Close buttons
            int labelY = BY + row4H + gutter;

            labelCloseTrades = new Rectangle(
                X + gutter,
                labelY,
                120,
                20
            );
            labelCloseOrders = new Rectangle(
                X + panelW - 140,
                labelY,
                120,
                20
            );

            // Row6 Buttons (immediately under the labels)
            int btnRowY = labelY + 20 + gutter;  // 20px label height + gutter

            btnAll = new Button("All",
                             X + gutter, btnRowY,
                             X + 60, btnRowY + row2H,
                             smallBack, smallPen, smallFont, textBrush);

            btnProfit = new Button("Profit",
                             X + 68, btnRowY,
                             X + 128, btnRowY + row2H,
                             smallBack, smallPen, smallFont, textBrush);

            btnLoss = new Button("Loss",
                             X + 136, btnRowY,
                             X + 196, btnRowY + row2H,
                             smallBack, smallPen, smallFont, textBrush);

            btnStop = new Button("Stop",
                             X + panelW - 68, btnRowY,
                             X + panelW - gutter, btnRowY + row2H,
                             smallBack, smallPen, smallFont, textBrush);




            // Subscribe for clicks if needed:
            CurrentChart.MouseClick += CurrentChart_MouseClick;
        }

        public override void Dispose()
        {
            CurrentChart.MouseClick -= CurrentChart_MouseClick;
            base.Dispose();
        }

        protected override void OnUpdate(UpdateArgs args) { }

        public override void OnPaintChart(PaintChartEventArgs args)
        {
            if (HistoricalData.Count != Count)
                return;

            var g = args.Graphics;
            var r = args.Rectangle;
            int X = xShift, Y = yShift;

            int panelBottom = btnStop.Y2 + gutter;
            // our rectangle starts at Y-4, so its height is panelBottom - (Y - 4)
            float panelHeight = panelBottom - (Y - 4);

            var rect = new RectangleF(
                X - 4,
                Y - 4,
                panelW + 8,
                panelHeight
            );

            // 1) Panel background & rounded border
            using (var path = new GraphicsPath())
            {
                float d = btnRadius;
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseAllFigures();

                using (var br = new SolidBrush(panelBack))
                    g.FillPath(br, path);
                g.DrawPath(new Pen(borderCol), path);
            }
            // 2) Header bar
            var hdr = new Rectangle(X, Y, panelW, headerH);
            using (var br = new SolidBrush(headerBack))
                g.FillRectangle(br, hdr);
            g.DrawString("Trade Manager", titleFont, textBrush,
                         X + panelW / 2, Y + headerH / 2, CenterFormat);

            // 3) Row1: SELL / qty / BUY
            sellBtn.Draw(g, btnRadius);
            buyBtn.Draw(g, btnRadius);
            g.DrawString(quantity.ToString(), mainFont, textBrush,
                         X + panelW / 2, Y + headerH + row1H / 2, CenterFormat);

            // 4) Row2: Pips bar
            var p2 = new Rectangle(X + gutter,
                Y + headerH + row1H,
                panelW - gutter * 2,
                row2H);
            g.DrawRectangle(Pens.Gray, p2);
            // left
            g.DrawString(pipL.ToString("F1"), mainFont, textBrush,
                         p2.X + (p2.Width / 3) / 2, p2.Y + row2H / 2, CenterFormat);
            // center
            g.DrawString("Pips", mainFont, textBrush,
                         p2.X + p2.Width / 2, p2.Y + row2H / 2, CenterFormat);
            // right
            g.DrawString(pipR.ToString("F1"), mainFont, textBrush,
                         p2.Right - (p2.Width / 3) / 2, p2.Y + row2H / 2, CenterFormat);

            // 5) Row3: Lot-Calc + Cash
            foreach (var b in lotRadios)
            {
                b.DrawCircle(g, btnRadius);
                g.DrawString(b.Text, smallFont, textBrush,
                    b.X1 + b.Width + 4,
                    b.Y1 + row3H / 2,
                    LeftFormat);
            }
            // draw the grey box
            using (var br = new SolidBrush(panelBack))
                g.FillRectangle(br, cashBox);
            g.DrawRectangle(Pens.Gray, cashBox);

            // draw the number centered
            g.DrawString(cashAmt.ToString("F2"), smallFont, textBrush,
                         cashBox.X + cashBox.Width / 2,
                         cashBox.Y + cashBox.Height / 2,
                         CenterFormat);

            // draw the “USD” just to the right of it
            g.DrawString("USD", smallFont, textBrush,
                         cashBox.Right + 4,  // small 4px gap
                         cashBox.Y + cashBox.Height / 2,
                         LeftFormat);


            // 6) Row4: Break-Even & Partial
            beBtn.Draw(g, btnRadius);
            g.DrawString(beVal.ToString("F1"), smallFont, textBrush,
                         beBtn.X2 + 30,
                         beBtn.Y1 + row4H / 2,
                         CenterFormat);
            partBtn.Draw(g, btnRadius);

            // 7) Row5: Labels (above buttons)
            g.DrawString("Close Trades", mainFont, textBrush,
                         labelCloseTrades, LeftFormat);
            g.DrawString("Close Orders", mainFont, textBrush,
                         labelCloseOrders, LeftFormat);

            // 8) Row6: All / Profit / Loss / Stop (below labels)
            btnAll.Draw(g, btnRadius);
            btnProfit.Draw(g, btnRadius);
            btnLoss.Draw(g, btnRadius);
            btnStop.Draw(g, btnRadius);




            // restore clip
            g.SetClip(r, CombineMode.Replace);
        }

        void CurrentChart_MouseClick(object _, ChartMouseNativeEventArgs __) { /* … */ }

        //── Button helper ───────────────────────────────────────────────────────────
        //── Button helper ────────────────────────────────────────────────────────────
        private class Button
        {
            public string Text;
            public int X1, Y1, X2, Y2;
            private readonly Brush back;
            private readonly Pen border;
            private readonly Font font;
            private readonly Brush txtBrush;
            private readonly bool isCircle;

            // <-- notice the final "bool circle = false" parameter
            public Button(
                string text,
                int x1, int y1,
                int x2, int y2,
                Brush back,
                Pen border,
                Font font,
                Brush txtBrush,
                bool circle = false    // default false, but allows you to pass circle:true
            )
            {
                Text = text;
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
                this.back = back;
                this.border = border;
                this.font = font;
                this.txtBrush = txtBrush;
                this.isCircle = circle;
            }

            public int Width => X2 - X1;
            public int Height => Y2 - Y1;

            public void Draw(Graphics g, int radius = 0)
            {
                if (isCircle)
                    return;

                using (var path = new GraphicsPath())
                {
                    float d = radius;
                    path.AddArc(X1, Y1, d, d, 180, 90);
                    path.AddArc(X2 - d, Y1, d, d, 270, 90);
                    path.AddArc(X2 - d, Y2 - d, d, d, 0, 90);
                    path.AddArc(X1, Y2 - d, d, d, 90, 90);
                    path.CloseAllFigures();

                    g.FillPath(back, path);
                    g.DrawPath(border, path);
                }

                g.DrawString(
                    Text,
                    font,
                    txtBrush,
                    X1 + Width / 2,
                    Y1 + Height / 2,
                    OrderPlacingTool.CenterFormat
                );
            }

            public void DrawCircle(Graphics g, int _)
            {
                var dia = Height;
                var rect = new Rectangle(X1, Y1, dia, dia);

                g.DrawEllipse(border, rect);
                if (isCircle)
                    g.FillEllipse(back, rect);
            }

            public bool Contains(int x, int y)
                => x >= X1 && x < X2 && y >= Y1 && y < Y2;
        }

    }
}
