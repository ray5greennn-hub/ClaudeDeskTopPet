// Claude 桌宠 —— 单文件 C# WinForms 程序
// Copyright (C) 2026 雷茂教主Ray5Green
// 本程序以 GPL-3.0 发布，详见仓库根目录的 LICENSE 文件。
// This program is free software under the GNU General Public License v3.0 — see LICENSE.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Program
{
    [DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main(string[] args)
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        if (args.Length == 2 && args[0] == "--render") { PetForm.Render(args[1]); return; }
        if (args.Length == 2 && args[0] == "--render-settings") { PetForm.RenderSettings(args[1]); return; }
        if (args.Length == 2 && args[0] == "--render-idles") { PetForm.RenderIdles(args[1]); return; }
        if (args.Length == 2 && args[0] == "--render-meal") { MealForm.RenderSelf(args[1]); return; }
        Application.Run(new PetForm());
    }
}

enum St { Fall, Jump, Walk, Idle, Sleep, Computer, Cling, Dance, Bubble, Stretch, Eat, Bye, GoNest, Gift, Play, Bath, Frozen, GoWater, Water, Meal, Nest }

class Particle
{
    public float X, Y, VX, VY;
    public int Life, Kind;      // Kind: 0 爱心, 1 小星星(带颜色), 2 文字, 3 泡泡
    public string Text;
    public Color Col;
    public Font F;
    public bool Bubble;   // 文字放在对话气泡里
}

// 小窝的样子
class NestLook
{
    internal int Type, Color, Pattern, Decor = 2, FlagColor, PlushL, PlushR;
    // 背景：场景、墙纸颜色、房间装饰、户外装饰、天气（0 = 自动）
    internal int Bg, Wall, RoomDecor = 3, OutDecor = 7, Weather;
    internal int Light = 1; internal bool LampOn = true;
    internal int GlowAlpha = 70, GlowSpread = 50, GlowSize = 50, GlowColor;
    internal bool GlowOn = true; internal int BgAlpha = 100;   // 小窝发光开关、背景透明度   // 小窝自带的主灯光   // 灯光：0 关 1 自动 2 一直开；落地灯开没开
    internal NestLook Clone() { return (NestLook)MemberwiseClone(); }
}

class PetForm : Form
{
    const int COLS = 12, ROWS = 12, TOP = 5;
    internal int SZ; internal float K;

    St state = St.Fall;
    int dir = 1;        // 走路方向
    int side = 0;       // 贴墙：-1 左墙, 1 右墙
    int vdir = 0;       // 贴墙时上下爬
    int snap = 0;       // 拖动中吸附到哪一侧
    bool pinned;        // 是用户亲手贴到墙上的
    bool follow;        // 跟着鼠标跑
    float px, py, vx, vy;
    int timer = 60, frame, tick, blink, happy, squash;
    int boost, hot, shake, full, lookDir, shiny;
    int foodIdx, bites, eatTick;
    float foodT, foodFromX, foodFromY;   // 食物飞到嘴边的进度（0..1）和起点
    List<int> meal = new List<int>(); int mealPos; int[] pendingMeal;   // 套餐
    MealForm mealForm;
    int giftAnim; bool giftDone;
    // 平时的小动作（St.Idle）：1 思考 2 溜溜球 3 喝热茶 4 蹦蹦跳跳 5 扫地 6 打招呼 7 打喷嚏
    // 贴墙时的待机（St.Cling 停住时）：1 张望 2 挥手 3 打盹 4 晃腿 5 哼歌 6 吃饼干
    int idleAct, clingAct, actT;
    // 在小窝里醒着做事：1 电脑 2 零食 3 看书 4 游戏机 5 看电影 6 听歌（0 = 睡觉）
    int nestAct, nestActT, nestAwake, nestSnack, nestBite; bool nestGoAwake, nestDraw;
    int movieGenre, nestReactT, tearT, scareT, nestFedPrev; bool nestFed, nestMealOn;
    List<int> nestQueue = new List<int>();   // 在窝里吃的套餐，一样一样来   // 看电影的类型（让玩家猜）、喂进来的零食          // 拆礼物的小动画
    bool drag, dragMoved;
    float offX, offY; Point down;
    int rot;            // 当前绘制朝向：0 正常, -1 脚朝左, 1 脚朝右
    int ox, oy;         // 绘制时的整体偏移（跳舞、摇头）

    readonly List<Particle> parts = new List<Particle>();
    readonly Random rand = new Random();
    readonly Brush orange = new SolidBrush(Color.FromArgb(217, 119, 87));
    readonly Brush hotBr = new SolidBrush(Color.FromArgb(232, 62, 48));
    readonly Brush dark = new SolidBrush(Color.FromArgb(25, 25, 25));
    readonly Brush pink = new SolidBrush(Color.FromArgb(240, 90, 120));
    readonly Brush gray = new SolidBrush(Color.FromArgb(110, 110, 140));
    readonly Brush bubbleBr = new SolidBrush(Color.FromArgb(140, 205, 255));
    readonly Brush lid = new SolidBrush(Color.FromArgb(55, 58, 70));
    readonly Brush lidEdge = new SolidBrush(Color.FromArgb(90, 94, 110));
    readonly Brush baseGray = new SolidBrush(Color.FromArgb(190, 192, 200));
    readonly Brush giftRed = new SolidBrush(Color.FromArgb(230, 75, 95));
    readonly Font zfont = new Font("Consolas", 9f, FontStyle.Bold);
    readonly Font tfont = new Font("SimSun", 9f);
    static readonly Color KeyColor = Color.FromArgb(255, 0, 255);
    static readonly Color Yellow = Color.FromArgb(255, 200, 60);
    static readonly Color PinkCol = Color.FromArgb(240, 90, 120);
    internal static readonly Color BodyCol = Color.FromArgb(217, 119, 87);
    static readonly string[] CodeBits = { "</>", "{ }", "01", "♪", "#", "?!", "=>", "~" };
    static readonly Color[] CodeCols = {
        Color.FromArgb(80, 140, 255), Color.FromArgb(60, 190, 120), Color.FromArgb(160, 110, 240) };

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);

    // ---------- 语言 ----------
    internal bool En;
    string T(string zh, string en) { return En ? en : zh; }

    // ---------- 装扮 ----------
    static readonly string[][] OutfitZh = {
        new[] { "不戴", "礼帽", "派对帽", "皇冠", "蝴蝶结", "毛线帽", "耳机", "草帽", "棒球帽", "厨师帽", "猫耳", "花环", "魔法帽", "光环" },
        new[] { "不戴", "墨镜", "圆眼镜", "腮红", "小胡子", "爱心眼镜", "星星眼镜", "单片眼镜", "方框眼镜", "眼罩", "口罩" },
        new[] { "不戴", "领结", "领带", "围巾", "铃铛项圈", "条纹围巾", "毛线围巾", "格子围巾", "珍珠项链", "领巾" },
        new[] { "不穿", "毛衣", "雨衣", "西装", "卫衣", "背带裤", "披风", "半穿风衣", "羽绒服", "牛仔外套", "水手服" } };
    static readonly string[][] OutfitEn = {
        new[] { "None", "Top hat", "Party hat", "Crown", "Bow", "Beanie", "Headphones", "Straw hat", "Baseball cap", "Chef hat", "Cat ears", "Flower crown", "Witch hat", "Halo" },
        new[] { "None", "Sunglasses", "Round glasses", "Blush", "Mustache", "Heart glasses", "Star glasses", "Monocle", "Square glasses", "Eye patch", "Face mask" },
        new[] { "None", "Bow tie", "Necktie", "Scarf", "Bell collar", "Striped scarf", "Knit scarf", "Plaid scarf", "Pearl necklace", "Bandana" },
        new[] { "None", "Sweater", "Raincoat", "Suit", "Hoodie", "Overalls", "Cape", "Trench coat (half on)", "Puffer jacket", "Denim jacket", "Sailor top" } };
    static readonly string[] SlotZh = { "帽子 / 头饰", "脸部", "脖子", "外套" };
    static readonly string[] SlotEn = { "Hat / Headwear", "Face", "Neck", "Coat" };
    internal const int SLOTS = 4;
    internal static int OutfitCount(int slot) { return OutfitZh[slot].Length; }
    internal static string OutfitName(int slot, int i, bool en) { return en ? OutfitEn[slot][i] : OutfitZh[slot][i]; }
    internal static string SlotName(int slot, bool en) { return en ? SlotEn[slot] : SlotZh[slot]; }

    int[] outfit = new int[SLOTS];
    internal int[] Outfit { get { return outfit; } }
    int accDx;
    readonly Brush accBlack = new SolidBrush(Color.FromArgb(30, 30, 35));
    readonly Brush accGray = new SolidBrush(Color.FromArgb(70, 70, 82));
    readonly Brush accRed = new SolidBrush(Color.FromArgb(215, 45, 55));
    readonly Brush accGold = new SolidBrush(Color.FromArgb(250, 200, 60));
    readonly Brush accGoldDark = new SolidBrush(Color.FromArgb(200, 145, 30));
    readonly Brush accPink = new SolidBrush(Color.FromArgb(250, 120, 165));
    readonly Brush accPinkDark = new SolidBrush(Color.FromArgb(215, 60, 120));
    readonly Brush accBlue = new SolidBrush(Color.FromArgb(70, 125, 225));
    readonly Brush accNavy = new SolidBrush(Color.FromArgb(35, 60, 140));
    readonly Brush accBrown = new SolidBrush(Color.FromArgb(70, 42, 30));
    readonly Brush accBlush = new SolidBrush(Color.FromArgb(240, 250, 95, 135));
    // 外套
    // 每件外套左右袖子的颜色（Empty = 没有袖子，露出小手）
    static readonly Color[] SleeveL = { Color.Empty, Color.FromArgb(205, 60, 70), Color.FromArgb(250, 205, 60),
        Color.FromArgb(45, 55, 90), Color.FromArgb(140, 145, 160), Color.Empty, Color.Empty,
        Color.FromArgb(200, 170, 110), Color.FromArgb(240, 140, 170), Color.FromArgb(80, 120, 190), Color.FromArgb(245, 245, 248) };
    static readonly Color[] SleeveR = { Color.Empty, Color.FromArgb(205, 60, 70), Color.FromArgb(250, 205, 60),
        Color.FromArgb(45, 55, 90), Color.FromArgb(140, 145, 160), Color.Empty, Color.Empty,
        Color.Empty, Color.FromArgb(240, 140, 170), Color.FromArgb(80, 120, 190), Color.FromArgb(245, 245, 248) };
    internal static Color SleeveColor(int coat, int side) { return side == 0 ? SleeveL[coat] : SleeveR[coat]; }
    Brush SleeveBr(int side)
    {
        Color c = SleeveColor(outfit[3], side);
        return c.IsEmpty ? null : C(c.R, c.G, c.B);
    }

    // 颜色画刷缓存
    static readonly Dictionary<int, Brush> BrCache = new Dictionary<int, Brush>();
    static Brush C(int r, int g, int b)
    {
        int key = (r << 16) | (g << 8) | b;
        Brush br;
        if (!BrCache.TryGetValue(key, out br)) { br = new SolidBrush(Color.FromArgb(r, g, b)); BrCache[key] = br; }
        return br;
    }
    // 按小图案（半格）画：X 表示填色
    void PH(Graphics g, Brush br, int x, int y, params string[] rows)
    {
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
                if (rows[r][c] == 'X') H(g, br, x + c, y + r, 1, 1);
    }
    readonly Brush coatRed = new SolidBrush(Color.FromArgb(205, 60, 70));
    readonly Brush coatRedDark = new SolidBrush(Color.FromArgb(165, 40, 55));
    readonly Brush coatYellow = new SolidBrush(Color.FromArgb(250, 205, 60));
    readonly Brush coatYellowDark = new SolidBrush(Color.FromArgb(215, 165, 35));
    readonly Brush coatNavy = new SolidBrush(Color.FromArgb(45, 55, 90));
    readonly Brush coatNavyDark = new SolidBrush(Color.FromArgb(28, 34, 60));
    readonly Brush coatGray = new SolidBrush(Color.FromArgb(140, 145, 160));
    readonly Brush coatGrayDark = new SolidBrush(Color.FromArgb(112, 117, 132));
    readonly Brush coatDenim = new SolidBrush(Color.FromArgb(80, 120, 190));
    readonly Brush coatCape = new SolidBrush(Color.FromArgb(200, 40, 55));

    // ---------- 小窝 ----------
    internal bool NestOn = true;
    internal NestLook Look = new NestLook();
    NestForm nest;
    bool inNest, intoNest;
    int nestX = int.MinValue, nestY;
    internal bool InNest { get { return inNest; } }

    // ---------- 洗澡 ----------
    BathForm bath;
    bool inBath, intoBath;
    internal bool InBath { get { return inBath; } }

    // ---------- 玩具 ----------
    ToyForm toy;
    int playTimer, kickAnim, kickCd, bounceCount, popCount, bubbleWait;
    bool playJump;
    // 食物特效
    int iceStreak, lastIceTick, frozenT, iceHP, shiver, bloat, milkT, sourT, seedT, coolT, rainbow, hiccups;
    // 退出动画：睡觉时 / 洗澡时
    int quitT; bool sleepyBye, bathBye, nestBye;
    // 摸头：鼠标在头上来回晃
    float petScore; int lastPetX, lastPetSign, pettedT, blushT, lastPetSay = -9999;
    internal bool Quitting { get { return quitT > 0 || state == St.Bye; } }
    bool chasing;

    // ---------- 礼物 ----------
    DateTime nextGiftRoll = DateTime.Now.AddHours(1);
    bool giftPending;
    int giftIdx, giftPhase;
    static readonly string[] GiftZh = { "小花", "四叶草", "小星星", "情书", "糖果" };
    static readonly string[][] Gifts = {
        new[]{ "...PP...", ".PPPPPP.", "PPPYYPPP", "PPPYYPPP", ".PPPPPP.", "...GG.G.", ".G.GGGG.", "..GGG..." },
        new[]{ ".gg..gg.", "gggggggg", ".gggggg.", "..gGGg..", ".gggggg.", "gggggggg", ".gg.Ggg.", "....G..." },
        new[]{ "...YY...", "...YY...", "YYYYYYYY", ".YYYYYY.", "..YYYY..", ".YYYYYY.", ".YY..YY.", "Y......Y" },
        new[]{ "........", "llllllll", "lqllllql", "llqllqll", "lllrrlll", "llrrrrll", "lllrrlll", "llllllll" },
        new[]{ "........", "........", "B..PP..B", "BBPwPPBB", "BBPPPPBB", "B..PP..B", "........", "........" },
    };

    SettingsForm settings;
    PetForm previewer;

    // ---------- 存档 ----------
    static string CfgDir
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudePet"); }
    }
    static int I(string s, int def) { int v; return int.TryParse(s.Trim(), out v) ? v : def; }
    static int Clamp(int v, int n) { return v >= 0 && v < n ? v : 0; }
    void LoadConfig()
    {
        try
        {
            string f = Path.Combine(CfgDir, "config.txt"), legacy = Path.Combine(CfgDir, "outfit.txt");
            string txt = File.Exists(f) ? File.ReadAllText(f) : (File.Exists(legacy) ? File.ReadAllText(legacy) : null);
            if (txt == null) return;
            if (txt.Contains("="))
            {
                foreach (string line in txt.Split('\n'))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "hat": outfit[0] = Clamp(I(v, 0), OutfitCount(0)); break;
                        case "face": outfit[1] = Clamp(I(v, 0), OutfitCount(1)); break;
                        case "neck": outfit[2] = Clamp(I(v, 0), OutfitCount(2)); break;
                        case "coat": outfit[3] = Clamp(I(v, 0), OutfitCount(3)); break;
                        case "lang": En = v == "en"; break;
                        case "nest": NestOn = v == "1"; break;
                        case "nestType": Look.Type = Clamp(I(v, 0), NestForm.TypeZh.Length); break;
                        case "nestColor": Look.Color = Clamp(I(v, 0), NestForm.Colors.Length); break;
                        case "nestSkin": Look.Pattern = Clamp(I(v, 0), NestForm.PatternZh.Length); break;
                        case "nestDecor": Look.Decor = I(v, 2) & 511; break;
                        case "flagColor": Look.FlagColor = Clamp(I(v, 0), NestForm.FlagColors.Length); break;
                        case "plushL": Look.PlushL = Clamp(I(v, 0), NestForm.PlushZh.Length); break;
                        case "plushR": Look.PlushR = Clamp(I(v, 0), NestForm.PlushZh.Length); break;
                        case "bg": Look.Bg = Clamp(I(v, 0), BackdropForm.SceneZh.Length); break;
                        case "bgWall": Look.Wall = Clamp(I(v, 0), BackdropForm.WallColors.Length); break;
                        case "bgRoom": Look.RoomDecor = I(v, 3) & 255; break;
                        case "bgOut": Look.OutDecor = I(v, 7) & 511; break;
                        case "glowOn": Look.GlowOn = v != "0"; break;
                        case "bgA": Look.BgAlpha = Math.Max(10, Math.Min(100, I(v, 100))); break;
                        case "glowA": Look.GlowAlpha = Math.Max(0, Math.Min(100, I(v, 70))); break;
                        case "glowS": Look.GlowSpread = Math.Max(0, Math.Min(100, I(v, 50))); break;
                        case "glowR": Look.GlowSize = Math.Max(0, Math.Min(100, I(v, 50))); break;
                        case "glowC": Look.GlowColor = Clamp(I(v, 0), LightForm.GlowColors.Length); break;
                        case "light": Look.Light = Clamp(I(v, 1), 3); break;
                        case "lamp": Look.LampOn = v != "0"; break;
                        case "weather": Look.Weather = Clamp(I(v, 0), BackdropForm.WeatherZh.Length); break;
                        case "nestX": nestX = I(v, int.MinValue); break;
                        case "nestY": nestY = I(v, 0); break;
                    }
                }
            }
            else
            {
                string[] s = txt.Trim().Split(',');
                for (int i = 0; i < 3 && i < s.Length; i++) outfit[i] = Clamp(I(s[i], 0), OutfitCount(i));
                if (s.Length > 3) En = s[3].Trim() == "en";
            }
        }
        catch { }
    }
    void SaveConfig()
    {
        try
        {
            Directory.CreateDirectory(CfgDir);
            if (nest != null && !nest.IsDisposed && nest.Visible) { nestX = (int)nest.x; nestY = (int)nest.y; }
            string s = "hat=" + outfit[0] + "\nface=" + outfit[1] + "\nneck=" + outfit[2] + "\ncoat=" + outfit[3]
                + "\nlang=" + (En ? "en" : "zh") + "\nnest=" + (NestOn ? "1" : "0")
                + "\nnestType=" + Look.Type + "\nnestColor=" + Look.Color + "\nnestSkin=" + Look.Pattern
                + "\nnestDecor=" + Look.Decor + "\nflagColor=" + Look.FlagColor
                + "\nplushL=" + Look.PlushL + "\nplushR=" + Look.PlushR
                + "\nbg=" + Look.Bg + "\nbgWall=" + Look.Wall + "\nbgRoom=" + Look.RoomDecor + "\nbgOut=" + Look.OutDecor + "\nweather=" + Look.Weather
                + "\nglowOn=" + (Look.GlowOn ? "1" : "0") + "\nbgA=" + Look.BgAlpha + "\nglowA=" + Look.GlowAlpha + "\nglowS=" + Look.GlowSpread + "\nglowR=" + Look.GlowSize + "\nglowC=" + Look.GlowColor
                + "\nlight=" + Look.Light + "\nlamp=" + (Look.LampOn ? "1" : "0");
            if (nestX != int.MinValue) s += "\nnestX=" + nestX + "\nnestY=" + nestY;
            File.WriteAllText(Path.Combine(CfgDir, "config.txt"), s + "\n");
        }
        catch { }
    }
    void RefreshSettings() { if (settings != null && !settings.IsDisposed) settings.RefreshAll(); }

    internal void SetOutfit(int slot, int idx) { outfit[slot] = idx; AfterOutfitChange(); }
    internal void RandomOutfit()
    {
        for (int s = 0; s < SLOTS; s++) outfit[s] = rand.Next(OutfitCount(s));
        AfterOutfitChange();
    }
    internal void ClearOutfit() { outfit = new int[SLOTS]; AfterOutfitChange(); }
    void AfterOutfitChange()
    {
        SaveConfig(); RefreshSettings();
        if (nest != null) nest.Invalidate();
        if (bath != null) bath.Invalidate();
        if (state == St.Bye) return;
        if (!inNest && state != St.Sleep) happy = 25;
        for (int k = 0; k < 6; k++)
            AddSpark(rand.Next(SZ, 10 * SZ), rand.Next(3 * SZ, 10 * SZ), k % 2 == 0 ? Yellow : PinkCol, 14);
    }
    internal void SetLang(bool en)
    {
        if (En == en) return;
        En = en; SaveConfig(); BuildMenu(); RefreshSettings();
        if (mealForm != null && !mealForm.IsDisposed) mealForm.Close();
        if (bath != null) bath.Invalidate();
    }

    internal void SetNestEnabled(bool on)
    {
        if (NestOn == on) return;
        NestOn = on;
        if (on) ShowNest(true); else HideNest();
        SaveConfig(); RefreshSettings();
    }
    internal void NestLookChanged() { SaveConfig(); if (nest != null) nest.Invalidate(); UpdateBackdrop(); RefreshSettings(); }

    // ---------- 背景 ----------
    BackdropForm backdrop;
    int autoWeather = 1, nextWeatherTick = 9000;
    internal int Tick { get { return tick; } }
    internal int Weather { get { return Look.Weather == 0 ? autoWeather : Look.Weather; } }
    void UpdateBackdrop()
    {
        bool want = Look.Bg > 0 && nest != null && !nest.IsDisposed && nest.Visible;
        if (!want) { if (backdrop != null && !backdrop.IsDisposed) backdrop.Hide(); return; }
        if (backdrop == null || backdrop.IsDisposed) backdrop = new BackdropForm(this, SZ);
        SyncBackdrop();
        backdrop.Opacity = Look.BgAlpha / 100.0;
        if (!backdrop.Visible) backdrop.Show();
        backdrop.Invalidate();
        RaiseZ();
    }
    internal void ToggleLamp()
    {
        Look.LampOn = !Look.LampOn; SaveConfig();
        if (backdrop != null) backdrop.Invalidate();
        UpdateLight(); RaiseZ();   // 点完灯，层级重新排好：灯光 > Claude > 小窝 > 背景
        RefreshSettings();
    }

    // ---------- 灯光（单独一层，盖在 Claude 上面，鼠标可以穿过） ----------
    LightForm light;
    int nextThunder = 300;
    internal void GlowChanged() { SaveConfig(); }
    internal void BgAlphaChanged() { SaveConfig(); if (backdrop != null && !backdrop.IsDisposed) backdrop.Opacity = Look.BgAlpha / 100.0; }   // 灯光层下一帧自己会重画
    internal float PreviewDark { get { return Look.Light == 0 ? 0 : Math.Max(0.35f, Darkness()); } }
    float Darkness()
    {
        if (Look.Light == 0) return 0;
        if (Look.Light == 2 || Look.Bg == 3) return 0.45f;
        int h = DateTime.Now.Hour;
        if (h >= 19 || h < 6) return 0.42f;   // 天黑了
        switch (Weather) { case 3: return 0.35f; case 2: return 0.2f; case 4: return 0.1f; }
        return 0;
    }
    void UpdateLight()
    {
        bool nestOk = nest != null && !nest.IsDisposed && nest.Visible;
        float dk = nestOk ? Darkness() : 0;
        bool lamp = nestOk && Look.Bg == 1 && (Look.RoomDecor & 8) != 0 && Look.LampOn;
        if (dk <= 0 && !lamp) { if (light != null && !light.IsDisposed && light.Visible) light.Hide(); return; }
        if (light == null || light.IsDisposed) light = new LightForm(SZ);
        int x = (int)(nest.x + nest.Width / 2 - light.Width / 2), y = (int)(nest.y + nest.Height - light.Height);
        bool fresh = !light.Visible;
        light.Render(Look, dk, lamp, (tick / 15) % 3, x, y);
        if (fresh) { light.Show(); RaiseZ(); }
    }

    void SyncBackdrop()
    {
        if (backdrop == null || backdrop.IsDisposed || nest == null) return;
        Point p = new Point((int)(nest.x + nest.Width / 2 - backdrop.Width / 2), (int)(nest.y + nest.Height - backdrop.Height));
        if (backdrop.Location != p) backdrop.Location = p;
    }
    internal void GoSleepNow() { GoSleep(); }

    // 只用于预览：用指定大小、指定装扮画一只站着的 Claude
    internal Bitmap Snapshot(int sz, int[] fit, bool closedEyes)
    {
        Bitmap bmp = new Bitmap(COLS * sz, ROWS * sz);
        using (Graphics g = Graphics.FromImage(bmp)) DrawPose(g, sz, fit, St.Idle, closedEyes);
        return bmp;
    }
    internal void DrawSleeper(Graphics g, int sz, int[] fit) { DrawPose(g, sz, fit, St.Sleep, false); }
    internal int NestLift(int sz) { return state == St.Nest ? -(3 * sz / 2) : 0; }   // 醒着时坐起来
    internal void DrawNester(Graphics g, int sz, int[] fit)
    {
        if (state != St.Nest) { DrawSleeper(g, sz, fit); return; }
        int sSZ = SZ, sl = lookDir; int[] sFit = outfit;
        SZ = sz; outfit = fit; rot = 0; ox = 0; oy = 0; lookDir = 0;
        state = St.Idle; nestDraw = true;
        DrawBody(g, 0);   // 手上的东西在 DrawBody 里一起画，跟着身体一起起伏
        state = St.Nest; nestDraw = false; SZ = sSZ; outfit = sFit; lookDir = sl; ox = 0; oy = 0;
    }
    Color MovieGlow()
    {
        switch (movieGenre)
        {
            case 0: return Color.FromArgb(255, 220, 120);                                           // 喜剧：暖黄
            case 1: return (tick / 5) % 9 == 0 ? Color.White : Color.FromArgb(150, 40, 55);          // 恐怖：暗红，偶尔一闪
            case 2: return Color.FromArgb(120, 160, 255);                                           // 悲伤：蓝
            case 3: return Color.FromArgb(255, 150, 200);                                           // 爱情：粉
            default: return (tick / 6) % 2 == 0 ? Color.FromArgb(255, 160, 60) : Color.FromArgb(255, 235, 180);   // 动作：橙色闪
        }
    }
    // 窝里抱在怀里的东西（半格坐标，画在身体里，跟着起伏）
    void DrawNestProp(Graphics g)
    {
        int B = 2 * TOP, half = SZ / 2, b = TOP;
        switch (nestAct)
        {
            case 1: case 5:   // 抱着笔记本电脑（背面朝外），屏幕的光照在脸上；看电影时光会跟着剧情变色
                {
                    Color gc = nestAct == 5 ? MovieGlow() : Color.FromArgb(150, 205, 255);
                    int a = nestAct == 5 ? 34 + 10 * ((tick / 7) % 3) : 16 + 8 * ((tick / 6) % 3);
                    using (Brush glow = new SolidBrush(Color.FromArgb(a, gc))) R(g, glow, 3 * SZ, (b + 1) * SZ, 6 * SZ, 2 * SZ);
                    H(g, nestAct == 5 ? C(175, 180, 195) : C(70, 72, 85), 4, B + 6, 16, 4);
                    H(g, nestAct == 5 ? C(145, 150, 165) : C(52, 54, 64), 4, B + 6, 16, 1);
                    H(g, (tick / 12) % 2 == 0 ? Brushes.White : C(170, 205, 255), 11, B + 8, 2, 1);
                }
                break;
            case 2:   // 抱着零食啃
                {
                    int fp = FoodPx(), mh = half + (int)Math.Round((1 + Math.Sin(TAU * ((tick / 2) % 5) / 5)) / 2 * half);
                    R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, mh);
                    DrawArt(g, Foods[nestSnack], 6 * SZ - 4 * fp + ox, (b + 3) * SZ + oy, fp, Math.Min(6, nestBite * 2));
                }
                break;
            case 3:   // 抱着一本摊开的书，过一会儿翻一页
                H(g, C(90, 130, 200), 4, B + 6, 16, 4);
                H(g, Brushes.White, 5, B + 6, 7, 3); H(g, Brushes.White, 12, B + 6, 7, 3);
                H(g, C(175, 175, 190), 6, B + 7, 5, 1); H(g, C(175, 175, 190), 13, B + 7, 5, 1);
                if (tick % 150 < 12) H(g, Brushes.White, 9 + (tick % 150) / 2, B + 4, 1, 2);   // 翻起来的那一页
                break;
            case 4:   // 掌机（背面），左蓝右红的手柄
                using (Brush glow = new SolidBrush(Color.FromArgb(22, ((tick / 5) % 3) * 100, 200, 255 - ((tick / 5) % 3) * 80)))
                    R(g, glow, 3 * SZ, (b + 1) * SZ, 6 * SZ, 2 * SZ);
                H(g, C(60, 62, 72), 7, B + 7, 10, 3);
                H(g, C(70, 150, 230), 4, B + 7, 3, 3);
                H(g, C(235, 80, 80), 17, B + 7, 3, 3);
                break;
            case 6:   // 戴着耳机听歌
                H(g, C(60, 60, 70), 5, B - 1, 14, 1);
                H(g, C(60, 60, 70), 4, B, 1, 2); H(g, C(60, 60, 70), 19, B, 1, 2);
                H(g, C(240, 90, 120), 3, B + 2, 2, 3); H(g, C(240, 90, 120), 19, B + 2, 2, 3);
                if ((tick / 10) % 2 == 0) R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, half);
                break;
        }
    }    internal void DrawBather(Graphics g, int sz, int[] fit, bool closedEyes) { DrawPose(g, sz, fit, St.Idle, closedEyes); }

    // 临时换成指定姿势画一遍，画完恢复
    void DrawPose(Graphics g, int sz, int[] fit, St st, bool closedEyes)
    {
        int sSZ = SZ; int[] sFit = outfit; St sSt = state;
        int sBlink = blink, sHappy = happy, sLook = lookDir, sSq = squash, sHot = hot, sShake = shake, sKick = kickAnim, sGift = giftPhase;
        bool sDrag = drag;
        SZ = sz; outfit = fit; state = st; blink = closedEyes ? 3 : 0; happy = 0; lookDir = 0; squash = 0;
        hot = 0; shake = 0; kickAnim = 0; drag = false; rot = 0; ox = 0; oy = 0;
        DrawBody(g, 0);
        SZ = sSZ; outfit = sFit; state = sSt; blink = sBlink; happy = sHappy; lookDir = sLook; squash = sSq;
        hot = sHot; shake = sShake; kickAnim = sKick; giftPhase = sGift; drag = sDrag; ox = 0; oy = 0;
    }

    void OpenSettings()
    {
        if (previewer == null) previewer = new PetForm(true);
        if (settings == null || settings.IsDisposed) settings = new SettingsForm(this, previewer, K);
        settings.Show();
        settings.Activate();
    }

    // ---------- 像素画（食物 / 礼物 / 玩偶）----------
    internal static readonly string[] FoodZh = { "苹果", "饼干", "蛋糕", "寿司", "披萨", "咖啡", "辣椒", "冰淇淋", "西瓜", "甜甜圈", "汉堡", "棒棒糖", "牛奶", "柠檬", "爆米花", "薄荷糖", "炸鸡", "薯条", "面条", "饭团", "热狗", "奶茶", "布丁", "草莓",
        "龙虾", "饺子", "煎蛋", "吐司", "香蕉", "葡萄", "桃子", "巧克力", "马卡龙", "烤串", "蛋挞", "可乐" };
    internal static readonly string[] FoodEn = { "Apple", "Cookie", "Cake", "Sushi", "Pizza", "Coffee", "Chili", "Ice cream", "Watermelon", "Donut", "Burger", "Lollipop", "Milk", "Lemon", "Popcorn", "Mint candy", "Fried chicken", "Fries", "Noodles", "Rice ball", "Hot dog", "Milk tea", "Pudding", "Strawberry",
        "Lobster", "Dumpling", "Fried egg", "Toast", "Banana", "Grapes", "Peach", "Chocolate", "Macaron", "Skewer", "Egg tart", "Cola" };
    const int COFFEE = 5, CHILI = 6, CAKE = 2, ICE = 7, WATERMELON = 8, DONUT = 9, BURGER = 10, LOLLI = 11, MILK = 12, LEMON = 13, POPCORN = 14, MINT = 15,
        FRIED = 16, FRIES = 17, NOODLES = 18, RICEBALL = 19, HOTDOG = 20, BOBA = 21, PUDDING = 22, STRAWBERRY = 23,
        SUSHI_I = 3, LOBSTER = 24, DUMPLING = 25, FRIEDEGG = 26, TOAST = 27, BANANA = 28, GRAPES = 29, PEACH = 30, CHOCO = 31, MACARON = 32, SKEWER = 33, EGGTART = 34, COLA = 35;
    internal static readonly string[][] Foods = {
        new[]{ "....b...", "...bgg..", ".rrrrrr.", "rrwrrrrr", "rrwrrrrr", "rrrrrrrr", ".rrrrrr.", "..rr.rr." },
        new[]{ "..cccc..", ".ccdccc.", "cccccdcc", "cdcccccc", "ccccdccc", "ccdccccc", ".ccccdc.", "..cccc.." },
        new[]{ "...r....", "..www...", ".pppppp.", "pppppppp", "yyyyyyyy", "wwwwwwww", "yyyyyyyy", "yyyyyyyy" },
        new[]{ "........", ".oooooo.", "oosoosoo", "oooooooo", "wwkkkkww", "wwkkkkww", "wwwwwwww", ".wwwwww." },
        new[]{ "eeeeeeee", "hhhhhhhh", ".hrrhhh.", ".hrrhhh.", "..hhhh..", "..hrrh..", "...hh...", "...hh..." },
        new[]{ "..S..S..", "...S..S.", "wwwwww..", "wnnnnwww", "wwwwww.w", "wwwwwwww", ".wwww...", "........" },
        new[]{ ".....gg.", "....g...", "...rr...", "..rrr...", ".rrrr...", ".rrr....", "rrr.....", "rr......" },
        new[]{ "..pppp..", ".ppwppp.", ".pppppp.", ".pppppp.", "tttttttt", ".tttttt.", "..tttt..", "...tt..." },
        new[]{ "........", "rrrrrrrr", "rkrrrkrr", ".rrkrrr.", ".rrrrkr.", "..rrrr..", "..wwww..", "...gg..." },   // 西瓜
        new[]{ "..tttt..", ".pppppp.", "pYpppBpp", "ppp..ppp", "ppp..ppp", "tpwpppYt", ".tttttt.", "..tttt.." },   // 甜甜圈
        new[]{ "..cccc..", ".cwccwc.", "cccwcccc", "gggggggg", "hhhhhhhh", "nnnnnnnn", "nnnnnnnn", ".cccccc." },   // 汉堡
        new[]{ ".PPPP...", "PwwwPP..", "PwPPwP..", "PwPwwP..", ".PPPP...", "...S....", "....S...", ".....S.." },   // 棒棒糖
        new[]{ "...SS...", "..wwww..", ".wwwwww.", ".wBBBBw.", ".wBwwBw.", ".wBBBBw.", ".wwwwww.", ".wwwwww." },   // 牛奶
        new[]{ "......gg", "..YYYYg.", ".YYhhYY.", "YYYYYYYY", "YhYYYYYY", "YYYYYYhY", ".YYYYYY.", "..YYYY.." },   // 柠檬
        new[]{ ".w.yw.w.", "wywwywyw", ".wwywww.", "rwrwrwrw", ".rwrwrw.", ".rwrwrw.", ".rwrwrw.", "..rwrw.." },   // 爆米花
        new[]{ "........", "..MMMM..", "S.MwwM.S", "SSwMMwSS", "SSwMMwSS", "S.MwwM.S", "..MMMM..", "........" },   // 薄荷糖
        new[]{ "........", "..eeee..", ".eeheee.", ".eeeehe.", "..eeeee.", "...eew..", "....ww..", "...w.w.." },   // 炸鸡腿
        new[]{ "..h.h.h.", ".hh.hhh.", ".hhhhhh.", "rrrrrrrr", ".rrrrrr.", ".rrwwrr.", ".rrrrrr.", "..rrrr.." },   // 薯条
        new[]{ "..S...S.", "...S...S", ".yhyhgy.", "wwwwwwww", "wrrrrrrw", ".wwwwww.", "..wwww..", "...ww..." },   // 面条
        new[]{ "........", "...ww...", "..wwww..", ".wwwwww.", ".wwwwww.", "wwkkkkww", "wwkkkkww", ".wkkkkw." },   // 饭团
        new[]{ "........", "........", ".tttttt.", "oYoYoYoo", "oooooooo", "tttttttt", ".tttttt.", "........" },   // 热狗
        new[]{ "....P...", "...P....", ".wwwwww.", ".UUUUUU.", ".UUUUUU.", ".UUkUkU.", ".UkUUkU.", "..UUUU.." },   // 奶茶
        new[]{ "........", "...dd...", "..dddd..", ".yyyyyy.", ".yyyyyy.", "yyyyyyyy", "yyyyyyyy", "SSSSSSSS" },   // 布丁
        new[]{ "...gg...", "..gggg..", ".rrrrrr.", "rryrrryr", ".rrrrrr.", ".ryrryr.", "..rrrr..", "...rr..." },   // 草莓
        new[]{ "r.r..r.r", "rr....rr", ".rr..rr.", "..rrrr..", "..kook..", "..rrrr..", "...rr...", "..rrrr.." },   // 龙虾（竖着，钳子朝上）
        new[]{ "........", "........", "..mmmm..", ".mqmqmm.", "mwwwwwwm", "mwwwwwwm", ".mmmmmm.", "........" },   // 饺子
        new[]{ "........", "..wwww..", ".wwwwww.", "wwwYYwww", "wwYYYYww", "wwwYYwww", ".wwwwww.", "..www..." },   // 煎蛋
        new[]{ ".cccccc.", "cmmmmmmc", "cmmmmmmc", "cmmmmmmc", "cmmmmmmc", "cmmmmmmc", "cmmmmmmc", "cccccccc" },   // 吐司
        new[]{ "......n.", ".....YY.", "....YYY.", "...YYYY.", "..YYYY..", ".YYYY...", "YYY.....", "n......." },   // 香蕉
        new[]{ "....g...", "...g....", "..LVLV..", ".VLVLVL.", "..VLVLV.", "...VLV..", "...LV...", "....V..." },   // 葡萄
        new[]{ "....gg..", "...g....", ".pppppp.", "pppPpppp", "pppPpppp", ".pppPpp.", "..pppp..", "........" },   // 桃子
        new[]{ "........", "dddddddd", "dnndnndd", "dddddddd", "dnndnndd", "rrrrrrrr", "rwwrrrrr", "rrrrrrrr" },   // 巧克力
        new[]{ "........", "..PPPP..", ".PPPPPP.", ".wwwwww.", ".PPPPPP.", "..PPPP..", "........", "........" },   // 马卡龙
        new[]{ "..eeee..", "..eeee..", "..gggg..", "..oooo..", "..oooo..", "..eeee..", "...SS...", "...SS..." },   // 烤串
        new[]{ "........", "........", ".eeeeee.", "eYYeYYYe", "eYYYYeYe", "eeeeeeee", ".eeeeee.", "........" },   // 蛋挞
        new[]{ "...S....", "....S...", "..wwww..", ".DDDDDD.", ".rrrrrr.", ".rwwrrr.", ".DDDDDD.", "..DDDD.." },   // 可乐
    };
    static readonly Dictionary<char, Brush> Pal = MakePal();
    static Dictionary<char, Brush> MakePal()
    {
        Dictionary<char, Brush> d = new Dictionary<char, Brush>();
        d['r'] = new SolidBrush(Color.FromArgb(225, 60, 55));
        d['g'] = new SolidBrush(Color.FromArgb(80, 175, 80));
        d['b'] = new SolidBrush(Color.FromArgb(120, 80, 50));
        d['w'] = new SolidBrush(Color.FromArgb(250, 250, 245));
        d['c'] = new SolidBrush(Color.FromArgb(222, 170, 100));
        d['d'] = new SolidBrush(Color.FromArgb(95, 60, 40));
        d['p'] = new SolidBrush(Color.FromArgb(250, 150, 180));
        d['y'] = new SolidBrush(Color.FromArgb(250, 215, 130));
        d['o'] = new SolidBrush(Color.FromArgb(250, 130, 90));
        d['s'] = new SolidBrush(Color.FromArgb(255, 200, 175));
        d['S'] = new SolidBrush(Color.FromArgb(205, 205, 210));
        d['k'] = new SolidBrush(Color.FromArgb(35, 45, 40));
        d['e'] = new SolidBrush(Color.FromArgb(200, 140, 70));
        d['h'] = new SolidBrush(Color.FromArgb(250, 205, 80));
        d['n'] = new SolidBrush(Color.FromArgb(110, 70, 45));
        d['t'] = new SolidBrush(Color.FromArgb(215, 165, 95));
        d['P'] = new SolidBrush(Color.FromArgb(245, 110, 160));
        d['Y'] = new SolidBrush(Color.FromArgb(255, 205, 60));
        d['G'] = new SolidBrush(Color.FromArgb(50, 140, 70));
        d['l'] = new SolidBrush(Color.FromArgb(250, 240, 225));
        d['q'] = new SolidBrush(Color.FromArgb(200, 175, 150));
        d['B'] = new SolidBrush(Color.FromArgb(110, 170, 240));
        d['u'] = new SolidBrush(Color.FromArgb(175, 120, 75));
        d['U'] = new SolidBrush(Color.FromArgb(235, 175, 135));
        d['m'] = new SolidBrush(Color.FromArgb(240, 212, 172));
        d['v'] = new SolidBrush(Color.FromArgb(160, 160, 172));
        d['O'] = new SolidBrush(BodyCol);
        d['M'] = new SolidBrush(Color.FromArgb(140, 225, 195));
        d['V'] = new SolidBrush(Color.FromArgb(140, 80, 190));
        d['L'] = new SolidBrush(Color.FromArgb(190, 140, 230));
        d['D'] = new SolidBrush(Color.FromArgb(80, 40, 30));
        return d;
    }
    internal static void DrawArt(Graphics g, string[] rows, int x, int y, int fp, int skipRows)
    {
        for (int r = skipRows; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                char ch = rows[r][c];
                if (ch == '.') continue;
                g.FillRectangle(Pal[ch], x + c * fp, y + r * fp, fp, fp);
            }
    }
    internal static Bitmap ArtIcon(string[] rows, int fp)
    {
        Bitmap bmp = new Bitmap(8 * fp, 8 * fp);
        using (Graphics g = Graphics.FromImage(bmp)) DrawArt(g, rows, 0, 0, fp, 0);
        return bmp;
    }
    static Image ToyIcon(int kind)
    {
        Bitmap bmp = new Bitmap(16, 16);
        using (Graphics g = Graphics.FromImage(bmp)) ToyForm.DrawToy(g, kind, 2, 0.3f);
        return bmp;
    }

    readonly Timer timerCtl = new Timer();

    public PetForm() : this(false) { }

    public PetForm(bool previewOnly)
    {
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) K = g.DpiX / 96f;
        SZ = Math.Max(4, 2 * (int)Math.Round(4 * K));   // 保持偶数，配饰可以用半格

        Text = "Claude Pet";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = KeyColor;
        TransparencyKey = KeyColor;
        DoubleBuffered = true;
        ClientSize = new Size(COLS * SZ, ROWS * SZ);
        if (previewOnly) return;

        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        px = wa.Left + rand.Next(100, Math.Max(101, wa.Width - 200));
        py = wa.Top;
        Location = new Point((int)px, (int)py);

        LoadConfig();
        BuildMenu();
        timerCtl.Interval = 33;
        timerCtl.Tick += delegate { Step(); };
    }

    void BuildMenu()
    {
        ContextMenuStrip menu = new ContextMenuStrip();

        ToolStripMenuItem feed = new ToolStripMenuItem(T("喂食", "Feed"));
        // 食物太多了，分成几类
        string[] catZh = { "主食", "小吃", "甜点", "水果", "饮料" }, catEn = { "Meals", "Snacks", "Sweets", "Fruit", "Drinks" };
        int[][] cats = {
            new[] { SUSHI_I, 4, BURGER, NOODLES, RICEBALL, HOTDOG, DUMPLING, FRIEDEGG, TOAST, FRIED, LOBSTER },
            new[] { 1, POPCORN, FRIES, SKEWER, CHILI, MINT },
            new[] { CAKE, ICE, DONUT, LOLLI, PUDDING, CHOCO, MACARON, EGGTART },
            new[] { 0, WATERMELON, LEMON, STRAWBERRY, BANANA, GRAPES, PEACH },
            new[] { COFFEE, MILK, BOBA, COLA } };
        for (int c = 0; c < cats.Length; c++)
        {
            ToolStripMenuItem sub = new ToolStripMenuItem(En ? catEn[c] : catZh[c], ArtIcon(Foods[cats[c][0]], 2));
            foreach (int i in cats[c])
            {
                int idx = i;
                sub.DropDownItems.Add(En ? FoodEn[i] : FoodZh[i], ArtIcon(Foods[i], 2), delegate { Feed(idx); });
            }
            feed.DropDownItems.Add(sub);
        }
        feed.DropDownItems.Add(new ToolStripSeparator());
        feed.DropDownItems.Add(T("套餐（最多 3 样）…", "Meal set (up to 3)…"), null, delegate { OpenMeal(); });
        menu.Items.Add(feed);

        ToolStripMenuItem act = new ToolStripMenuItem(T("动作", "Actions"));
        FillActMenu(act);
        act.DropDownOpening += delegate { FillActMenu(act); };   // 在小窝里时换成窝里的动作
        menu.Items.Add(act);

        ToolStripMenuItem game = new ToolStripMenuItem(T("玩游戏", "Games"));
        game.DropDownItems.Add(T("踢足球", "Soccer"), ToyIcon(0), delegate { StartToy(0); });
        game.DropDownItems.Add(T("玩毛线球", "Yarn ball"), ToyIcon(1), delegate { StartToy(1); });
        game.DropDownItems.Add(T("顶气球", "Keep the balloon up"), ToyIcon(2), delegate { StartToy(2); });
        game.DropDownItems.Add(T("扔飞盘", "Frisbee"), ToyIcon(3), delegate { StartToy(3); });
        game.DropDownItems.Add(T("戳泡泡", "Pop the bubbles"), ToyIcon(4), delegate { StartToy(4); });
        game.DropDownItems.Add(T("激光笔", "Laser pointer"), ToyIcon(5), delegate { StartToy(5); });
        game.DropDownItems.Add(new ToolStripSeparator());
        ToolStripItem putAway = game.DropDownItems.Add(T("收起玩具", "Put the toy away"), null, delegate { PutAwayToy(); });
        game.DropDownOpening += delegate { putAway.Enabled = toy != null; };
        menu.Items.Add(game);

        ToolStripItem bathItem = menu.Items.Add(T("洗澡", "Take a bath"));
        bathItem.Click += delegate { if (inBath || intoBath) EndBath(false); else StartBath(); };
        menu.Opening += delegate { bathItem.Text = (inBath || intoBath) ? T("结束洗澡", "End the bath") : T("洗澡", "Take a bath"); };

        ToolStripMenuItem glowItem = new ToolStripMenuItem(T("小窝的灯", "Bed light"));
        glowItem.Click += delegate { Look.GlowOn = !Look.GlowOn; NestLookChanged(); };
        menu.Opening += delegate { glowItem.Checked = Look.GlowOn; glowItem.Visible = NestOn; };
        menu.Items.Add(glowItem);

        ToolStripMenuItem fol = new ToolStripMenuItem(T("跟着鼠标跑", "Follow the mouse"));
        fol.CheckOnClick = true;
        fol.Checked = follow;
        fol.CheckedChanged += delegate { follow = fol.Checked; };
        menu.Items.Add(fol);

        menu.Items.Add(T("设置（装扮 / 小窝 / 语言）…", "Settings (Dress up / Bed / Language)…")).Click += delegate { OpenSettings(); };

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(T("拜拜（退出）", "Bye (Quit)")).Click += delegate { SayBye(); };

        ContextMenuStrip old = ContextMenuStrip;
        ContextMenuStrip = menu;
        if (nest != null) nest.ContextMenuStrip = menu;
        if (toy != null) toy.ContextMenuStrip = menu;
        if (bath != null) bath.ContextMenuStrip = menu;
        if (old != null) old.Dispose();
    }

    void FillActMenu(ToolStripMenuItem act)
    {
        act.DropDownItems.Clear();
        if (inNest)
        {
            string[] zh = { "", "敲代码", "吃零食", "看书", "打游戏", "看电影", "听歌" };
            string[] en = { "", "Code", "Have a snack", "Read a book", "Play games", "Watch a movie", "Listen to music" };
            for (int i = 1; i <= 6; i++)
            {
                int a = i;
                ToolStripMenuItem it = new ToolStripMenuItem(T(zh[i], en[i]));
                it.Checked = state == St.Nest && nestAct == i;
                it.Click += delegate { if (inNest) { nestAwake = 0; StartNestAct(a); } };
                act.DropDownItems.Add(it);
            }
            act.DropDownItems.Add(new ToolStripSeparator());
            act.DropDownItems.Add(state == St.Sleep ? T("叫醒", "Wake up") : T("在窝里睡觉 Zz", "Nap in bed Zz"), null, delegate
            {
                if (!inNest) return;
                if (state == St.Sleep) NestWakeUp(); else NestDoze();
            });
            act.DropDownItems.Add(T("跳出小窝", "Hop out of bed"), null, delegate { if (inNest) WakeFromNest(); });
            return;
        }
        act.DropDownItems.Add(T("跳一下 ♥", "Jump ♥"), null, delegate { Poke(); });
        act.DropDownItems.Add(T("玩电脑", "Use the computer"), null, delegate { Do(St.Computer, 700); });
        act.DropDownItems.Add(T("跳舞", "Dance"), null, delegate { Do(St.Dance, 300); });
        act.DropDownItems.Add(T("吹泡泡", "Blow bubbles"), null, delegate { Do(St.Bubble, 300); });
        act.DropDownItems.Add(T("伸懒腰", "Stretch"), null, delegate { Do(St.Stretch, 46); });
        {
            ToolStripMenuItem small = new ToolStripMenuItem(T("小动作", "Little things"));
            string[] zh = { "", "想一想", "玩溜溜球", "喝热茶", "蹦蹦跳跳", "扫地", "打招呼", "打喷嚏" };
            string[] en = { "", "Think", "Yo-yo", "Sip hot tea", "Hop around", "Sweep", "Say hi", "Sneeze" };
            for (int i = 1; i < zh.Length; i++)
            {
                int a = i;
                small.DropDownItems.Add(T(zh[i], en[i]), null, delegate { DoIdleAct(a); });
            }
            act.DropDownItems.Add(small);
        }
        if (state == St.Cling)
        {
            ToolStripMenuItem wall = new ToolStripMenuItem(T("在墙上…", "On the wall…"));
            string[] zh = { "", "东张西望", "挥挥手", "打个盹", "晃晃腿", "哼歌", "吃饼干" };
            string[] en = { "", "Look around", "Wave", "Doze off", "Swing legs", "Hum a tune", "Eat a cookie" };
            for (int i = 1; i < zh.Length; i++)
            {
                int a = i;
                wall.DropDownItems.Add(T(zh[i], en[i]), null, delegate { if (state == St.Cling) StartClingAct(a); });
            }
            act.DropDownItems.Add(wall);
        }
        act.DropDownItems.Add(T("去睡觉 / 叫醒", "Sleep / Wake up"), null, delegate
        {
            if (state == St.Sleep) Wake(); else GoSleep();
        });
        act.DropDownItems.Add(T("去小窝里玩 ♪", "Hang out in the bed ♪"), null, delegate { GoNestNow(); });
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80; return cp; }  // 不出现在 Alt+Tab
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); timerCtl.Start(); }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (NestOn) ShowNest(false);
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveConfig();
        if (nest != null) nest.Close();
        if (backdrop != null) backdrop.Close();
        if (light != null) light.Close();
        if (bath != null) bath.Close();
        base.OnFormClosed(e);
    }

    // 让桌宠盖在小窝 / 浴缸上面，玩具（附属窗口）会自动在桌宠上面
    // 右键菜单开着的时候不要动层级，免得灯光盖住菜单
    bool MenuOpen()
    {
        ContextMenuStrip m = ContextMenuStrip;
        if (m == null) return false;
        if (m.Visible) return true;
        foreach (ToolStripItem it in m.Items)
        {
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null && mi.HasDropDownItems && mi.DropDown.Visible) return true;
        }
        return false;
    }
    void RaiseZ()
    {
        if (!IsHandleCreated || MenuOpen()) return;
        // 叠放顺序：背景 < 小窝 < Claude < 灯光 < 玩具。每个都直接插到上一层的下面，中途不会有谁跑到 Claude 上面（拖小窝不闪）
        IntPtr top = new IntPtr(-1);
        bool lt = light != null && !light.IsDisposed && light.Visible && light.IsHandleCreated;
        if (lt) { SetWindowPos(light.Handle, top, 0, 0, 0, 0, 0x13); SetWindowPos(Handle, light.Handle, 0, 0, 0, 0, 0x13); }
        else SetWindowPos(Handle, top, 0, 0, 0, 0, 0x13);
        bool nt = nest != null && !nest.IsDisposed && nest.Visible && nest.IsHandleCreated;
        if (nt) SetWindowPos(nest.Handle, Handle, 0, 0, 0, 0, 0x13);
        if (backdrop != null && !backdrop.IsDisposed && backdrop.Visible && backdrop.IsHandleCreated)
            SetWindowPos(backdrop.Handle, nt ? nest.Handle : Handle, 0, 0, 0, 0, 0x13);
        if (toy != null && !toy.IsDisposed && toy.IsHandleCreated) SetWindowPos(toy.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
        // 设置、套餐这些窗口永远在桌宠上面，不会被挡住
        foreach (Form fm in new Form[] { settings, mealForm })
            if (fm != null && !fm.IsDisposed && fm.Visible && fm.IsHandleCreated) SetWindowPos(fm.Handle, top, 0, 0, 0, 0, 0x13);
    }

    bool OnGround()
    {
        return state == St.Walk || state == St.Idle || state == St.Sleep || state == St.Computer
            || state == St.Dance || state == St.Bubble || state == St.Stretch || state == St.Eat
            || state == St.GoNest || state == St.Gift || state == St.Play || state == St.Frozen
            || state == St.GoWater || state == St.Water || state == St.Meal;
    }
    bool Busy() { return state == St.Bye || inBath || intoBath || quitT > 0 || state == St.Frozen; }

    // ---------- 行为 ----------
    void Do(St s, int t)
    {
        if (Busy()) return;
        if (state == St.Sleep) { Wake(); return; }
        if (inNest) { if (s == St.Computer) StartNestAct(1); else WakeFromNest(); return; }
        if (!OnGround()) return;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        state = s; timer = t; frame = 0;
    }

    void Pick()
    {
        if (toy != null && rand.Next(100) < 75) { state = St.Play; playTimer = rand.Next(400, 900); return; }
        if (rand.Next(100) < 6 && NestUsable() && !nest.drag) { nestGoAwake = true; state = St.GoNest; timer = 900; return; }   // 回窝里玩一会儿
        if (rand.Next(100) < 10 && CanWater()) { state = St.GoWater; timer = 600; return; }   // 去给小窝的盆栽浇水
        int r = rand.Next(100);
        if (r < 34) { state = St.Walk; dir = rand.Next(2) == 0 ? 1 : -1; timer = rand.Next(60, 220); }
        else if (r < 44) { state = St.Idle; idleAct = 0; timer = rand.Next(40, 140); }
        else if (r < 62) StartIdleAct(rand.Next(1, 8));
        else if (r < 73) { state = St.Computer; timer = rand.Next(300, 700); frame = 0; }
        else if (r < 80) { state = St.Dance; timer = rand.Next(150, 300); frame = 0; }
        else if (r < 87) { state = St.Bubble; timer = rand.Next(150, 300); frame = 0; }
        else if (r < 93) { state = St.Stretch; timer = 46; }
        else { state = St.Idle; GoSleep(); }
    }

    // ---------- 平时的小动作 ----------
    void StartIdleAct(int a)
    {
        state = St.Idle; idleAct = a; actT = 0; frame = 0;
        int[] len = { 0, 170, 260, 300, 150, 300, 110, 130 };
        timer = len[a];
    }
    void DoIdleAct(int a)
    {
        if (Busy() || inNest) return;
        if (state == St.Sleep) { Wake(); return; }
        if (!OnGround()) return;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        StartIdleAct(a);
    }
    void IdleActStep()
    {
        actT++;
        Color brown = Color.FromArgb(150, 100, 60);
        switch (idleAct)
        {
            case 1:   // 思考：… → 灯泡亮了
                if (actT == 110) { AddText(T("有了!", "Got it!"), Color.FromArgb(220, 160, 30), tfont); AddSpark(10 * SZ, SZ, Yellow, 16); }
                break;
            case 2:   // 溜溜球
                if (actT == 2) AddText(T("看我的~", "Watch this~"), PinkCol, tfont);
                break;
            case 3:   // 喝热茶
                if (actT % 90 == 80) AddText(T("呼~ 好暖", "Ahh~ warm"), brown, tfont);
                break;
            case 4:   // 原地蹦蹦跳跳
                if (actT % 18 == 0) { squash = 3; if (rand.Next(3) == 0) AddText(T("嘿咻~", "Hup~"), PinkCol, tfont); }
                break;
            case 5:   // 扫地
                if (tick % 7 == 0) AddSpark(rand.Next(0, 3 * SZ), 11 * SZ, Color.FromArgb(185, 180, 170), 10);
                if (actT % 140 == 30) AddText(T("扫扫~", "Sweep sweep~"), brown, tfont);
                break;
            case 6:   // 打招呼
                if (actT == 2) AddText(T("嗨~", "Hi~"), PinkCol, tfont);
                break;
            case 7:   // 打喷嚏
                if (actT == 15) AddText(T("啊…", "Ah…"), Color.FromArgb(120, 120, 160), tfont);
                if (actT == 45) AddText(T("啊…啊…", "Ah… ah…"), Color.FromArgb(120, 120, 160), tfont);
                if (actT == 72)
                {
                    AddText(T("阿嚏!", "Achoo!"), Color.FromArgb(90, 150, 220), tfont);
                    shake = 14; squash = 4;
                    for (int k = 0; k < 6; k++) AddSpark(rand.Next(3 * SZ, 9 * SZ), (TOP + 3) * SZ, Color.FromArgb(200, 230, 255), 12);
                }
                if (actT == 100) blushT = 40;
                break;
        }
    }

    // ---------- 贴在墙上的待机 ----------
    void ClingActStep()
    {
        actT++;
        switch (clingAct)
        {
            case 1: if (actT == 60 && rand.Next(2) == 0) SayHead("?", Color.FromArgb(120, 120, 170), tfont, false); break;
            case 2: if (actT == 2) SayHead(T("嗨~", "Hi~"), PinkCol, tfont, true); break;
            case 3: if (actT % 40 == 1) SayHead("z", Color.FromArgb(150, 150, 205), zfont, false); break;
            case 4: case 5:
                if (actT % 22 == 0) SayHead(rand.Next(2) == 0 ? "♪" : "♫", CodeCols[rand.Next(CodeCols.Length)], zfont, false);
                break;
            case 6:
                if (actT % 45 == 5) { Particle p = new Particle(); p.Kind = 5; p.Col = Color.FromArgb(200, 150, 90); PlaceHead(p, 8 * SZ, (TOP + 4) * SZ, 0); p.Life = 18; parts.Add(p); }
                if (actT == 3) SayHead(T("偷偷吃块饼干~", "Sneaky cookie~"), Color.FromArgb(170, 110, 60), tfont, true);
                break;
        }
    }
    // 说话 / 冒字：贴墙时身体是横着的，字要冒在“头顶”那一侧
    void SayHead(string s, Color c, Font f, bool bubble)
    {
        AddText(s, c, f, bubble);
        if (state != St.Cling || side == 0) return;
        Particle p = parts[parts.Count - 1];
        float w;
        using (Graphics g = CreateGraphics()) w = g.MeasureString(s, f).Width;
        PlaceHead(p, p.X + w / 2, p.Y, w);
    }
    void PlaceHead(Particle p, float sx, float sy, float w)
    {
        int W = COLS * SZ;
        if (state != St.Cling || side == 0) { p.X = sx - w / 2; p.Y = sy; return; }
        float x = side < 0 ? W - sy : sy, y = side < 0 ? sx : W - sx;
        p.X = Math.Max(0, Math.Min(W - w, x - w / 2)); p.Y = y;
    }

    bool NestUsable()
    {
        if (nest == null || nest.IsDisposed || !nest.Visible) return false;
        Point pc = new Point((int)(px + Width / 2), (int)(py + Height / 2));
        Point nc = new Point((int)(nest.x + nest.Width / 2), (int)(nest.y + nest.Height / 2));
        return Screen.FromPoint(pc).Bounds == Screen.FromPoint(nc).Bounds;
    }
    float NestPetX() { return nest.x + (nest.Width - Width) / 2f; }

    // 浇水时站的位置：喷壶正好在小窝左边的盆栽上方
    float WaterX() { return nest.x + 0.9f * SZ; }
    bool CanWater()
    {
        if (!NestUsable() || (Look.Decor & 128) == 0 || nest.drag || nest.y < nest.Ground() - 1) return false;
        Rectangle wa = Screen.FromPoint(new Point((int)(px + Width / 2), (int)(py + Height / 2))).WorkingArea;
        float tx = WaterX();
        return tx >= wa.Left && tx <= wa.Right - Width;
    }
    bool CanPet()
    {
        return !drag && quitT == 0 && !inBath && !intoBath && state != St.Bye && state != St.Frozen
            && state != St.Jump && state != St.Fall;
    }

    // 睡觉：有小窝就走过去钻进去，没有就原地睡；只有被点一下才会醒
    void GoSleep()
    {
        if (inNest && state == St.Nest) { NestDoze(); return; }
        if (Busy() || state == St.Sleep || !OnGround()) return;
        nestGoAwake = false;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        if (NestUsable()) { state = St.GoNest; timer = 900; }
        else { state = St.Sleep; frame = 0; }
    }

    void Wake()
    {
        if (inNest) { if (state == St.Sleep) NestWakeUp(); else WakeFromNest(); return; }
        state = St.Idle; timer = 50; happy = 25; AddHearts(2);
    }

    void WakeFromNest()
    {
        inNest = false;
        state = St.Jump; vy = -9f * K; vx = (rand.Next(2) == 0 ? -1 : 1) * 3f * K;
        happy = 30; AddHearts(2);
        if (nest != null) nest.Invalidate();
        RaiseZ();
    }

    void ShowNest(bool nearPet)
    {
        if (nest == null || nest.IsDisposed) { nest = new NestForm(this, SZ, K); nest.ContextMenuStrip = ContextMenuStrip; }
        bool placed = false;
        if (!nearPet && nestX != int.MinValue)
        {
            Point c = new Point(nestX + nest.Width / 2, nestY + nest.Height / 2);
            foreach (Screen s in Screen.AllScreens)
                if (s.WorkingArea.Contains(c)) placed = true;
            if (placed) { nest.x = nestX; nest.y = nestY; }
        }
        if (!placed)
        {
            Rectangle wa = Screen.FromPoint(new Point((int)(px + Width / 2), (int)(py + Height / 2))).WorkingArea;
            float nx = px + Width + 2 * SZ;
            if (nx > wa.Right - nest.Width) nx = px - nest.Width - 2 * SZ;
            nest.x = Math.Max(wa.Left, nx); nest.y = wa.Top;
        }
        nest.vy = 0;
        nest.Location = new Point((int)nest.x, (int)nest.y);
        nest.Show();
        UpdateBackdrop();
        RaiseZ();
    }

    void HideNest()
    {
        if (inNest && state == St.Nest) { state = St.Fall; vy = 0; vx = 0; }
        inNest = false; intoNest = false;   // 在窝里睡着的话，就地继续睡
        if (state == St.GoNest) { state = St.Idle; timer = 40; }
        if (nest != null) nest.Hide();
        if (backdrop != null && !backdrop.IsDisposed) backdrop.Hide();
        if (light != null && !light.IsDisposed) light.Hide();
    }

    internal void OnNestMoved()
    {
        if (inNest)
        {
            px = NestPetX(); py = nest.y;
            Location = new Point((int)px, (int)py);
        }
        SyncBackdrop(); UpdateLight();   // 背景和灯光跟着一起走，不等下一帧
        RaiseZ();
    }
    internal void OnNestDropped() { SaveConfig(); RaiseZ(); }
    internal void OnNestClicked() { if (inNest) NestClick(); RaiseZ(); }

    // 点小窝里的 Claude：睡着 → 醒过来在窝里玩；醒着 → 跳出来
    void NestClick()
    {
        if (state == St.Sleep) NestWakeUp(); else WakeFromNest();
    }
    void NestWakeUp()
    {
        nestAwake = 0; happy = 25; AddHearts(1);
        AddText(T("醒啦~", "Morning~"), PinkCol, tfont);
        StartNestAct(0);
    }
    internal void GoNestNow()
    {
        if (Busy()) return;
        if (inNest) { if (state == St.Sleep) NestWakeUp(); return; }
        if (state == St.Sleep) Wake();
        if (!OnGround() || !NestUsable()) return;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        nestGoAwake = true; state = St.GoNest; timer = 900;
    }
    static readonly int[] NestSnacks = { 0, 1, 3, 9 };
    void StartNestAct(int a) { StartNestAct(a, false); }
    void StartNestAct(int a, bool quiet)
    {
        int old = nestAct;
        if (a <= 0) { do a = 1 + rand.Next(6); while (a == old); }
        nestAct = a; state = St.Nest; frame = 0;
        nestActT = rand.Next(1200, 3600);   // 一件事做 40 秒 ~ 2 分钟
        nestBite = 0; nestSnack = NestSnacks[rand.Next(NestSnacks.Length)]; nestFed = false;
        if (a == 5) { movieGenre = rand.Next(5); nestReactT = rand.Next(200, 450); }   // 这次看什么片子？不告诉你
        if (quiet) { if (nest != null) nest.Invalidate(); return; }
        string[] zh = { "", "敲代码~", "吃点零食~", "看会儿书~", "打游戏咯!", "看电影~", "听听歌~" };
        string[] en = { "", "Coding~", "Snack time~", "Reading~", "Game on!", "Movie time~", "Music~" };
        AddText(T(zh[a], en[a]), Color.FromArgb(120, 110, 170), tfont);
        if (nest != null) nest.Invalidate();
    }
    // 看电影的反应：喜剧笑、恐怖吓得发抖、悲伤掉眼泪、爱情脸红、动作片激动
    void MovieReact()
    {
        string[][] zh = { new[] { "哈哈哈", "噗嗤", "笑死我了", "哈哈哈哈!" }, new[] { "!!", "呜哇!", "不要开门…", "好可怕…" },
                          new[] { "呜呜…", "好感人…", "(吸鼻子)", "不要走…" }, new[] { "哇~", "好甜~", "亲上了!", "心动~" }, new[] { "哇!", "好燃!", "轰!", "太帅了!" } };
        string[][] en = { new[] { "Haha", "Pfft", "So funny", "Hahaha!" }, new[] { "!!", "Eek!", "Don't open it…", "So scary…" },
                          new[] { "Sniff…", "So touching…", "(sniffle)", "Don't go…" }, new[] { "Aww~", "So sweet~", "They kissed!", "Heart flutter~" }, new[] { "Whoa!", "So hype!", "Boom!", "So cool!" } };
        int k = rand.Next(4), lift = 3 * SZ / 2;
        string s = T(zh[movieGenre][k], en[movieGenre][k]);
        switch (movieGenre)
        {
            case 0: happy = 45; AddText(s, Color.FromArgb(220, 160, 30), tfont, false); break;
            case 1:
                scareT = 50; AddText(s, Color.FromArgb(160, 60, 75), tfont, false);
                AddSpark(9 * SZ, (TOP + 1) * SZ - lift, Color.FromArgb(130, 190, 255), 14);   // 冷汗
                break;
            case 2: tearT = 160; AddText(s, Color.FromArgb(90, 130, 210), tfont, false); break;
            case 3: blushT = 120; AddHearts(2); AddText(s, PinkCol, tfont, false); break;
            default:
                for (int i = 0; i < 4; i++) AddSpark(rand.Next(2 * SZ, 10 * SZ), rand.Next(2 * SZ, 5 * SZ) - lift, i % 2 == 0 ? Color.FromArgb(255, 150, 50) : Yellow, 12);
                AddText(s, Color.FromArgb(230, 120, 40), tfont, false);
                break;
        }
    }

    void NextNestBite()
    {
        nestAct = 2; state = St.Nest;
        nestSnack = nestQueue[0]; nestQueue.RemoveAt(0);
        nestBite = 0; nestFed = true; nestActT = Math.Max(nestActT, 900);
        if (nest != null) nest.Invalidate();
    }

    void NestDoze()
    {
        nestAct = 0; state = St.Sleep; frame = 0; nestAwake = 0;
        AddText(T("困了… Zz", "Sleepy… Zz"), Color.FromArgb(120, 120, 170), tfont);
        if (nest != null) nest.Invalidate();
    }
    // 窝里醒着的时候每一帧
    void NestStep()
    {
        nestAwake++;
        if (--nestActT <= 0)
        {
            if (nestAwake > 5400 && rand.Next(100) < 50) { NestDoze(); return; }   // 玩了 3 分钟以上，有点困了
            StartNestAct(0); return;
        }
        int lift = 3 * SZ / 2;
        switch (nestAct)
        {
            case 1: if (tick % 24 == 0) AddCode(); break;
            case 2:
                if (tick % 14 == 0)
                {
                    nestBite++;
                    AddSpark(5 * SZ, (TOP + 4) * SZ - lift, Crumb(nestSnack), 8);
                    if (nestBite >= 4)
                    {
                        nestBite = 0;
                        if (nestFed)   // 喂进来的那份吃完了：出现这个食物的反应，然后接着做刚才的事
                        {
                            nestFed = false; MealFx(nestSnack);
                            if (nestQueue.Count > 0) { NextNestBite(); return; }   // 套餐还有下一样
                            if (nestMealOn)
                            {
                                nestMealOn = false; happy = 50; AddHearts(3);
                                AddText(T("吃饱啦~ 谢谢款待!", "Yum! Thanks for the meal!"), PinkCol, tfont);
                            }
                            StartNestAct(nestFedPrev, true);
                            return;
                        }
                        if (rand.Next(3) == 0) nestSnack = NestSnacks[rand.Next(NestSnacks.Length)];
                    }
                }
                break;
            case 3: if (tick % 150 == 0) AddText(T("翻一页~", "flip~"), Color.FromArgb(120, 110, 170), tfont, false); break;
            case 4:
                if (tick % 10 == 0) AddSpark(rand.Next(3 * SZ, 9 * SZ), (TOP + 2) * SZ - lift, CodeCols[rand.Next(CodeCols.Length)], 8);
                if (rand.Next(450) == 0) { happy = 40; AddText(T("赢啦!", "I won!"), Yellow, tfont); }
                break;
            case 5:
                if (--nestReactT <= 0) { nestReactT = rand.Next(240, 640); MovieReact(); }
                break;
            case 6: if (tick % 18 == 0) AddText(rand.Next(2) == 0 ? "♪" : "♫", CodeCols[rand.Next(CodeCols.Length)], zfont, false); break;
        }
    }

    // ---------- 洗澡 ----------
    void StartBath()
    {
        if (state == St.Bye || inBath || intoBath) return;
        if (inNest) { inNest = false; if (nest != null) nest.Invalidate(); }
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        Rectangle wa = Screen.FromPoint(new Point((int)(px + Width / 2), (int)(py + Height / 2))).WorkingArea;
        bath = new BathForm(this, SZ, K);
        bath.ContextMenuStrip = ContextMenuStrip;
        float bx = px + Width / 2f - BathForm.PetCol * SZ - Width / 2f;
        bx = Math.Max(wa.Left, Math.Min(wa.Right - bath.Width, bx));
        bath.x = bx; bath.y = wa.Bottom - bath.Height;
        bath.Location = new Point((int)bath.x, (int)bath.y);
        bath.Show();
        RaiseZ();
        drag = false; side = 0; vdir = 0;
        float tx = bath.x + BathForm.PetCol * SZ;
        state = St.Jump; vy = -10f * K; vx = (tx - px) / 20f; intoBath = true;
        AddText(T("洗澡咯~", "Bath time~"), Color.FromArgb(80, 150, 230), tfont);
    }

    internal void EndBath(bool finished)
    {
        if (bath != null) { BathForm b = bath; bath = null; b.Close(); }
        bool was = inBath;
        inBath = false; intoBath = false;
        if (state == St.Bye) return;
        if (was) { state = St.Jump; vy = -10f * K; vx = (rand.Next(2) == 0 ? -1 : 1) * 3f * K; }
        if (finished) { shiny = 300; happy = 40; AddHearts(4); }
    }
    internal void BathSay(string zh, string en) { AddText(T(zh, en), Color.FromArgb(60, 140, 220), tfont); }
    internal void BathHearts(int n) { AddHearts(n); }

    void PickCling()
    {
        int r = rand.Next(100);
        clingAct = 0; actT = 0;
        if (!pinned && r < 10) { HopOff(); return; }
        if (r < 32) { vdir = -1; timer = rand.Next(30, 120); }
        else if (r < 52) { vdir = 1; timer = rand.Next(30, 120); }
        else if (r < 62) { vdir = 0; timer = rand.Next(40, 120); }
        else { vdir = 0; StartClingAct(rand.Next(1, 7)); }   // 在墙上待一会儿，做点小事
    }
    void StartClingAct(int a)
    {
        clingAct = a; actT = 0; vdir = 0;
        int[] len = { 0, 160, 110, 420, 200, 220, 260 };
        timer = len[a];
    }

    void HopOff()
    {
        state = St.Jump; vy = -7f * K; vx = -side * 5f * K; side = 0; vdir = 0;
    }

    void Poke()
    {
        if (state == St.Frozen)   // 冻住了：点几下把冰敲碎
        {
            if (quitT > 0) return;
            iceHP--;
            for (int k = 0; k < 5; k++) AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(3 * SZ, 11 * SZ), Color.FromArgb(200, 235, 255), 12);
            if (iceHP <= 0) Thaw(true); else AddText(T("咔!", "Crack!"), Color.FromArgb(80, 150, 220), tfont);
            return;
        }
        if (Busy()) return;
        if (state == St.Gift) { if (giftPhase == 0) OpenGift(); else AddHearts(2); return; }
        if (state == St.Meal) { happy = 20; AddHearts(1); return; }   // 吃饭时点它不会跳起来
        if (state == St.Sleep) { Wake(); return; }
        if (state == St.Cling) { HopOff(); happy = 30; AddHearts(3); }
        else if (OnGround()) { state = St.Jump; vy = -11f * K; vx = 0; happy = 30; AddHearts(3); }
    }

    void SayBye()
    {
        if (state == St.Bye || quitT > 0) return;
        if (settings != null && !settings.IsDisposed) settings.Close();
        if (mealForm != null && !mealForm.IsDisposed) mealForm.Close();
        drag = false; snap = 0;
        if (inBath || intoBath)   // 洗澡时：在浴缸里挥手，浴缸一起淡出
        {
            bathBye = true; quitT = 80;
            if (bath != null) bath.ByeBubbles();
            AddText(T("洗完澡再见~", "Bye, splash~"), Color.FromArgb(60, 140, 220), tfont);
            RaiseZ();
            return;
        }
        if (state == St.Sleep)    // 睡觉时：迷迷糊糊地半睁眼，慢慢抬手说拜拜
        {
            sleepyBye = true; quitT = 95;
            AddText(T("唔…拜拜…", "Mm… bye…"), Color.FromArgb(120, 120, 170), tfont);
            RaiseZ();
            return;
        }
        if (inNest && state == St.Nest)   // 窝里醒着：放下手里的东西，在窝里挥手，和小窝一起淡出
        {
            nestBye = true; quitT = 80;
            AddText(T("拜拜~", "Bye~"), PinkCol, tfont);
            RaiseZ();
            return;
        }
        if (inNest) { inNest = false; if (nest != null) nest.Invalidate(); }
        if (state != St.Cling) side = 0;
        state = St.Bye; timer = 60; frame = 0; vdir = 0;
        AddText(T("拜拜~", "Bye~"), PinkCol, tfont);
        RaiseZ();
    }

    void Feed(int idx)
    {
        if (Busy()) return;
        if (inNest && state == St.Nest)   // 在窝里：当零食吃
        {
            nestFedPrev = nestAct == 2 ? 0 : nestAct; nestAct = 2; nestSnack = idx; nestBite = 0; nestFed = true; nestActT = Math.Max(nestActT, 600);
            AddText(T("嚼嚼~", "Munch~"), PinkCol, tfont);
            return;
        }
        if (state == St.Sleep) { Wake(); return; }
        if (!OnGround()) return;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        if (full >= 6)   // 吃撑了，摇头拒绝
        {
            state = St.Idle; timer = 70; shake = 36;
            AddText(T("饱啦~", "So full~"), PinkCol, tfont);
            return;
        }
        state = St.Eat; foodIdx = idx;
        float tx, ty; FoodTarget(out tx, out ty);
        StartFood(tx, -8 * FoodPx());   // 从天上掉下来
    }

    void StartFood(float fromX, float fromY)
    {
        bites = 0; eatTick = 0; frame = 0; foodT = 0; foodFromX = fromX; foodFromY = fromY;
    }

    // 食物的吃法：0 嚼着吃, 1 端杯子喝, 2 吸管吸, 3 舔, 4 一颗颗扔进嘴里, 5 整颗含住
    static int FoodStyle(int i)
    {
        switch (i)
        {
            case COFFEE: return 1;
            case MILK: case BOBA: case COLA: return 2;
            case FRIES: case GRAPES: return 7;   // 一根一根 / 一颗一颗吃
            case LOBSTER: return 8;              // 整只吞进去
            case NOODLES: return 6;   // 拿筷子嗦面
            case ICE: case LOLLI: return 3;
            case POPCORN: return 4;
            case MINT: return 5;
            default: return 0;
        }
    }
    int BitesNeeded() { int s = FoodStyle(foodIdx); return (s == 4 || s == 7) ? 6 : (s == 3 ? 5 : 4); }
    int BiteGap() { int s = FoodStyle(foodIdx); return (s == 1 || s == 2) ? 20 : (s == 6 || s == 8 ? 18 : (s == 0 ? 14 : 12)); }
    int SeatDy() { return state == St.Meal && !drag ? SZ / 2 : 0; }   // 坐下来时身体低半格
    int MouthY() { return (TOP + 3) * SZ + SeatDy(); }

    // 每种吃法，食物最后停在哪里
    void FoodTarget(out float x, out float y)
    {
        int fp = FoodPx(), half = SZ / 2;
        float my = MouthY(), maxY = ROWS * SZ - 8 * fp;
        switch (FoodStyle(foodIdx))
        {
            case 1: x = 6 * SZ; y = my - 2 * fp; break;                        // 杯口贴着嘴
            case 2: x = 7 * SZ; y = Math.Min(maxY, my + half); break;          // 牛奶盒在旁边，插吸管
            case 3: x = 6 * SZ + half - (foodIdx == ICE ? fp : 0); y = my - 2 * fp; break;   // 举在嘴边舔
            default: x = (COLS * SZ - 8 * fp) / 2; y = Math.Min(maxY, my + SZ); break;
        }
    }

    static readonly Dictionary<int, Color> CrumbCache = new Dictionary<int, Color>();
    static Color Crumb(int i)   // 食物碎屑的颜色 = 这个食物用得最多的颜色
    {
        Color c;
        if (CrumbCache.TryGetValue(i, out c)) return c;
        Dictionary<char, int> cnt = new Dictionary<char, int>();
        foreach (string r in Foods[i])
            foreach (char ch in r)
                if (ch != '.') { int v; cnt.TryGetValue(ch, out v); cnt[ch] = v + 1; }
        char best = 'w'; int bv = -1;
        foreach (KeyValuePair<char, int> kv in cnt) if (kv.Value > bv) { bv = kv.Value; best = kv.Key; }
        c = ((SolidBrush)Pal[best]).Color; CrumbCache[i] = c;
        return c;
    }

    // 吃一帧；返回 true = 这一份吃完了
    bool EatStep()
    {
        if (foodT < 1) { foodT = Math.Min(1f, foodT + (state == St.Meal ? 1 / 14f : 1 / 24f)); return false; }
        eatTick++;
        if (eatTick % 5 == 0) frame++;
        int st = FoodStyle(foodIdx), gap = BiteGap(), sd = SeatDy();
        if (st == 4 && eatTick % gap == 1)   // 爆米花往上一抛
        {
            Particle p = new Particle();
            p.Kind = 5; p.X = 6 * SZ - SZ / 4; p.Y = (TOP + 4) * SZ + sd; p.Life = 9;
            p.VX = 0; p.VY = -2.6f * K; p.Col = Color.FromArgb(255, 250, 235);
            parts.Add(p);
        }
        if (eatTick % gap != 0) return false;
        bites++;
        Color cc = Crumb(foodIdx);
        switch (st)
        {
            case 0: case 4:
                AddSpark(5 * SZ, (TOP + 4) * SZ + sd, cc, 8);
                AddSpark(6 * SZ, (TOP + 4) * SZ + sd, cc, 8);
                break;
            case 1: case 2:
                if (bites % 2 == 1) AddText(T("咕嘟", "gulp"), Color.FromArgb(110, 140, 200), tfont, false);
                break;
            case 6:
                if (bites % 2 == 1) AddText(T("嗦~", "Slurp~"), Color.FromArgb(200, 150, 60), tfont, false);
                break;
            case 7:
                AddSpark(6 * SZ, MouthY(), cc, 6);
                break;
            case 8:
                if (bites == 2) AddText(T("啊呜~", "Aaam~"), Color.FromArgb(220, 80, 60), tfont, false);
                break;
            case 3:
                AddSpark(7 * SZ, MouthY(), cc, 8);
                if (bites == 2) AddText(T("舔舔~", "lick~"), PinkCol, tfont, false);
                break;
            case 5:
                if (bites == 1) AddText(T("啵!", "Pop!"), Color.FromArgb(60, 170, 150), tfont, false);
                break;
        }
        return bites >= BitesNeeded();
    }

    // ---------- 套餐 ----------
    void OpenMeal()
    {
        if (mealForm == null || mealForm.IsDisposed) mealForm = new MealForm(this, K, En);
        mealForm.Show();
        mealForm.Activate();
    }

    internal void ServeMeal(int[] items)
    {
        if (items == null || items.Length == 0 || Busy()) return;
        if (inNest)   // 在窝里：就在窝里一样一样抱着吃
        {
            if (state == St.Sleep) NestWakeUp();
            nestFedPrev = nestAct == 2 ? 0 : nestAct;
            nestQueue = new List<int>(items);
            nestMealOn = true;
            NextNestBite();
            AddText(T("在窝里开饭啦~", "Dinner in bed~"), PinkCol, tfont);
            return;
        }
        if (state == St.Sleep) Wake();
        if (!OnGround() || state == St.Meal || state == St.Eat) { pendingMeal = items; return; }   // 落地 / 吃完再端上来
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        if (full >= 6)
        {
            state = St.Idle; timer = 70; shake = 36;
            AddText(T("饱啦~", "So full~"), PinkCol, tfont);
            return;
        }
        meal = new List<int>(items); mealPos = 0; state = St.Meal; vx = 0; squash = 4;
        AddText(T("开饭啦~", "Dinner time~"), PinkCol, tfont);
        NextMealFood();
    }

    // 餐盘上还在等的食物摆在左右两边，中间空出来给正在吃的那份（k: 第几个, r: 还剩几个）
    int TraySlotX(int k, int r) { return (r >= 2 && k == 0 ? 3 : 9) * SZ - 4 * FoodPx(); }
    int TrayY() { return ROWS * SZ - SZ / 2; }

    void NextMealFood()
    {
        foodIdx = meal[mealPos];
        int sx = mealPos == 0 ? 6 * SZ - 4 * FoodPx() : TraySlotX(0, meal.Count - mealPos);
        StartFood(sx, TrayY() - 8 * FoodPx());
    }

    // 套餐里的食物：只留下好玩的小效果，不打断吃饭
    void MealFx(int idx)
    {
        full++;
        switch (idx)
        {
            case COFFEE: boost = Math.Max(boost, 300); AddText("!!", Yellow, zfont, false); break;
            case CHILI: hot = Math.Max(hot, 120); AddText(T("辣!", "Hot!"), Color.FromArgb(240, 70, 50), tfont, false); break;
            case ICE: shiver = Math.Max(shiver, 40); break;
            case WATERMELON: seedT = 50; break;
            case BURGER: bloat = Math.Max(bloat, 200); full++; break;
            case LOLLI: rainbow = Math.Max(rainbow, 60); break;
            case MILK: milkT = 450; break;
            case LEMON: sourT = Math.Max(sourT, 50); break;
            case POPCORN: PopcornBurst(); break;
            case MINT: coolT = Math.Max(coolT, 90); break;
            case LOBSTER: bloat = Math.Max(bloat, 150); AddText(T("一口吞!", "Gulp!"), Color.FromArgb(220, 80, 60), tfont, false); break;
            case COLA: AddText(T("嗝~", "Burp~"), Color.FromArgb(150, 100, 70), tfont, false); break;
            default: AddHearts(2); happy = Math.Max(happy, 20); break;
        }
    }

    void EndMeal()
    {
        meal.Clear(); mealPos = 0;
        state = St.Jump; vy = -7f * K; vx = 0; happy = 50; AddHearts(4);
        AddText(T("吃饱啦~ 谢谢款待!", "Yum! Thanks for the meal!"), PinkCol, tfont);
    }

    void FinishEat()
    {
        full++;
        switch (foodIdx)
        {
            case COFFEE:
                boost = 600; state = St.Walk; timer = 220; dir = rand.Next(2) == 0 ? 1 : -1;
                AddText("!!", Yellow, zfont);
                break;
            case CHILI:
                hot = 240; boost = 240; state = St.Walk; timer = 240; dir = rand.Next(2) == 0 ? 1 : -1;
                AddText(T("辣!!", "Hot!!"), Color.FromArgb(240, 70, 50), tfont);
                break;
            case ICE:   // 30 秒内连吃 3 个冰淇淋就会被冻住
                iceStreak = (iceStreak > 0 && tick - lastIceTick < 900) ? iceStreak + 1 : 1;
                lastIceTick = tick;
                if (iceStreak >= 3) { iceStreak = 0; Freeze(); break; }
                state = St.Dance; timer = 140; frame = 0; AddHearts(2);
                AddText(T("好冰~", "Brr~"), Color.FromArgb(90, 160, 230), tfont);
                break;
            case CAKE: case DONUT:
                state = St.Dance; timer = 160; frame = 0; AddHearts(3);
                break;
            case WATERMELON:   // 吐西瓜籽
                seedT = 70; state = St.Idle; timer = 75;
                AddText(T("噗噗~", "Ptoo~"), Color.FromArgb(60, 150, 70), tfont);
                break;
            case BURGER:       // 吃撑了，身体胖一圈
                bloat = 220; full++; state = St.Idle; timer = 80;
                AddText(T("饱饱~", "So stuffed~"), Color.FromArgb(200, 130, 60), tfont);
                break;
            case LOLLI:        // 彩虹闪闪 + 跳舞
                rainbow = 90; state = St.Dance; timer = 150; frame = 0;
                break;
            case MILK:         // 牛奶胡子
                milkT = 450; state = St.Jump; vy = -8f * K; vx = 0; happy = 30;
                AddText(T("咕嘟咕嘟~", "Gulp gulp~"), Color.FromArgb(110, 140, 200), tfont);
                break;
            case LEMON:        // 酸得发抖
                sourT = 90; state = St.Idle; timer = 95;
                AddText(T("好酸!", "So sour!"), Color.FromArgb(200, 170, 30), tfont);
                break;
            case POPCORN:      // 一蹦一蹦打嗝
                hiccups = 4; state = St.Jump; vy = -6f * K; vx = 0; PopcornBurst();
                AddText(T("嗝!", "Hic!"), Color.FromArgb(200, 150, 60), tfont);
                break;
            case LOBSTER:
                bloat = 150; state = St.Jump; vy = -9f * K; vx = 0; happy = 50; AddHearts(3);
                for (int k = 0; k < 8; k++) AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(2 * SZ, 9 * SZ), k % 2 == 0 ? Yellow : Color.FromArgb(240, 90, 70), 16);
                AddText(T("一口吞! 满足~", "Gulped it whole!"), Color.FromArgb(220, 80, 60), tfont);
                break;
            case COLA:
                state = St.Idle; timer = 60; happy = 20;
                for (int k = 0; k < 3; k++) AddBubble();
                AddText(T("嗝~", "Burp~"), Color.FromArgb(150, 100, 70), tfont);
                break;
            case NOODLES:
                state = St.Idle; timer = 70; happy = 40; AddHearts(2);
                AddText(T("嗦~ 好满足!", "Slurp~ so good!"), Color.FromArgb(200, 150, 60), tfont);
                break;
            case FRIED:
                state = St.Jump; vy = -8f * K; vx = 0; happy = 30;
                AddText(T("咔嚓! 好香~", "Crunch! Yum~"), Color.FromArgb(200, 130, 50), tfont);
                break;
            case BOBA:
                state = St.Dance; timer = 120; frame = 0;
                AddText(T("Q弹~", "Chewy~"), Color.FromArgb(160, 110, 70), tfont);
                break;
            case MINT:         // 呼出清凉的气
                coolT = 120; state = St.Idle; timer = 95;
                AddText(T("好清凉~", "So fresh~"), Color.FromArgb(60, 170, 150), tfont);
                break;
            default:
                state = St.Jump; vy = -8f * K; vx = 0; happy = 30; AddHearts(3);
                break;
        }
    }

    void Freeze()
    {
        state = St.Frozen; frozenT = 300; iceHP = 3; frame = 0;
        AddText(T("冻住了!", "Frozen!"), Color.FromArgb(80, 150, 230), tfont);
        for (int k = 0; k < 8; k++) AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(2 * SZ, 11 * SZ), Color.White, 16);
    }

    void Thaw(bool broken)
    {
        state = St.Idle; timer = 70; frozenT = 0; shiver = 90;
        if (broken)
        {
            AddText(T("解放啦!", "Free!"), Color.FromArgb(80, 150, 230), tfont);
            for (int k = 0; k < 12; k++) AddSpark(rand.Next(0, 12 * SZ), rand.Next(3 * SZ, 12 * SZ), Color.FromArgb(190, 230, 255), 16);
        }
        else AddText(T("化了~ 好冷", "Thawed~ brr"), Color.FromArgb(80, 150, 230), tfont);
    }

    void PopcornBurst()
    {
        for (int k = 0; k < 4; k++)
        {
            Particle p = new Particle();
            p.Kind = 5; p.X = rand.Next(3 * SZ, 9 * SZ); p.Y = (TOP + 2) * SZ;
            p.VX = ((float)rand.NextDouble() - 0.5f) * 3f * K; p.VY = -(2f + (float)rand.NextDouble() * 2f) * K;
            p.Life = 30; p.Col = k % 2 == 0 ? Color.FromArgb(255, 250, 235) : Color.FromArgb(250, 215, 110);
            parts.Add(p);
        }
    }

    int FoodPx() { return Math.Max(2, SZ / 3); }

    // ---------- 礼物 ----------
    void StartGift()
    {
        giftPending = false; state = St.Gift; giftPhase = 0; frame = 0;
        giftIdx = rand.Next(Gifts.Length); timer = 2700;   // 等你来拆，约 90 秒后它会自己打开
        AddText(T("有礼物给你!", "Gift for you!"), PinkCol, tfont);
    }

    void OpenGift()
    {
        giftPhase = 1; timer = 600; happy = 60; giftAnim = 0; giftDone = false;   // 举着等你来点
        AddHearts(4);
        for (int k = 0; k < 8; k++)
            AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(SZ, 6 * SZ), k % 2 == 0 ? Yellow : PinkCol, 18);
        AddText(En ? "For you!" : "送你" + GiftZh[giftIdx] + "~", PinkCol, tfont);
    }

    // 点头顶的礼物：花和四叶草转圈，星星闪光，情书拆开，糖果剥开
    void PlayGift()
    {
        if (giftAnim > 0) return;
        timer = Math.Max(timer, 260); happy = Math.Max(happy, 30);
        switch (giftIdx)
        {
            case 0: giftAnim = 45; AddText(T("转圈圈~", "Spin~"), PinkCol, tfont); break;
            case 1: giftAnim = 45; AddText(T("幸运加倍!", "Double luck!"), Color.FromArgb(50, 140, 70), tfont); break;
            case 2: giftAnim = 30; AddText(T("闪闪发光~", "Twinkle~"), Color.FromArgb(220, 160, 30), tfont); break;
            case 3:
                if (giftDone) { giftAnim = 20; AddHearts(2); }
                else { giftAnim = 50; giftDone = true; AddText(T("拆开看看~", "Open it~"), PinkCol, tfont); }
                break;
            case 4:
                if (giftDone) { giftAnim = 20; AddHearts(2); AddText(T("甜甜的~", "Sweet~"), PinkCol, tfont); }
                else { giftAnim = 40; giftDone = true; }
                break;
        }
    }

    void GiftAnimStep()
    {
        giftAnim--;
        int fp = SZ / 2;
        float cx = COLS * SZ / 2f, cy = (TOP - 4) * SZ - SZ / 2 + 4 * fp;
        switch (giftIdx)
        {
            case 0: case 1:
                if (giftAnim % 6 == 0)
                    AddSpark(cx + rand.Next(-3 * SZ, 3 * SZ), cy + rand.Next(-2 * SZ, 2 * SZ),
                        giftIdx == 0 ? Color.FromArgb(245, 110, 160) : Color.FromArgb(80, 175, 80), 16);
                break;
            case 2:
                if (giftAnim % 3 == 0) AddSpark(cx + rand.Next(-4 * SZ, 4 * SZ), cy + rand.Next(-3 * SZ, 2 * SZ), giftAnim % 6 == 0 ? Yellow : Color.White, 14);
                break;
            case 3:
                if (giftAnim == 12 && giftDone && timer > 0) { AddText(T("最喜欢你啦!", "Love you lots!"), PinkCol, tfont); AddHearts(3); }
                break;
            case 4:
                if (giftAnim == 15 && giftDone)   // 糖纸飞走
                {
                    for (int k = 0; k < 6; k++)
                    {
                        Particle p = new Particle();
                        p.Kind = 5; p.X = cx + rand.Next(-2 * SZ, 2 * SZ); p.Y = cy;
                        p.VX = ((float)rand.NextDouble() - 0.5f) * 4f * K; p.VY = -(1.5f + (float)rand.NextDouble() * 2f) * K;
                        p.Life = 28; p.Col = k % 2 == 0 ? Color.FromArgb(110, 170, 240) : Color.FromArgb(160, 205, 250);
                        parts.Add(p);
                    }
                    AddText(T("剥开啦~ 甜甜的!", "Unwrapped~ sweet!"), PinkCol, tfont);
                }
                break;
        }
    }

    // ---------- 玩具 / 游戏 ----------
    void StartToy(int kind)
    {
        if (Busy()) return;
        if (toy != null) { toy.Close(); toy = null; }
        int fp = kind == 5 ? Math.Max(2, SZ / 2) : Math.Max(3, SZ * 3 / 4);
        toy = new ToyForm(this, kind, fp, K);
        toy.x = px + Width / 2f + dir * 3 * SZ - toy.Width / 2f;
        toy.y = py + 2 * SZ;
        toy.vy = -7f * K; toy.vx = dir * 2f * K;
        if (kind == 2) { toy.y = py; toy.vy = -4f * K; toy.vx = dir * 1f * K; }
        toy.ContextMenuStrip = ContextMenuStrip;
        toy.Sync();
        AddOwnedForm(toy);
        if (kind == 4) SpawnBubble();
        toy.Show();
        bounceCount = 0; popCount = 0; bubbleWait = 0;
        if (kind == 3) AddText(T("扔飞盘给我~", "Throw it!"), PinkCol, tfont);
        if (inNest) WakeFromNest();
        else if (state == St.Sleep) Wake();
        else if (OnGround())
        {
            if (state == St.Gift && giftPhase == 0) giftPending = true;
            state = St.Play; playTimer = rand.Next(500, 900);
        }
    }

    void PutAwayToy()
    {
        if (toy != null) { toy.Close(); toy = null; }
        if (state == St.Play) { state = St.Idle; timer = 40; }
    }

    // 新泡泡：出现在离 Claude 远一点的地方，不会一出来就被碰破
    void SpawnBubble()
    {
        Rectangle wa = Screen.FromPoint(new Point((int)(px + Width / 2), (int)(py + Height / 2))).WorkingArea;
        int side = rand.Next(2) == 0 ? -1 : 1;
        float bx = px + Width / 2f + side * rand.Next(8 * SZ, 16 * SZ);
        if (bx < wa.Left + toy.Width || bx > wa.Right - toy.Width) bx = px + Width / 2f - side * rand.Next(8 * SZ, 16 * SZ);
        toy.x = Math.Max(wa.Left, Math.Min(wa.Right - toy.Width, bx - toy.Width / 2f));
        toy.y = wa.Bottom - toy.Height - rand.Next(0, 2 * SZ);
        toy.Sync();
    }

    void PopBubble(bool byUser)
    {
        if (bubbleWait > 0) return;   // 已经破了，在等下一个
        popCount++;
        Point o = Location;
        for (int k = 0; k < 6; k++)
            AddSpark(toy.x + toy.Width / 2f - o.X + rand.Next(-SZ, SZ), toy.y + toy.Height / 2f - o.Y + rand.Next(-SZ, SZ), Color.FromArgb(160, 210, 245), 12);
        if (popCount % 5 == 0) { AddText(popCount + T("个啦!", " pops!"), Color.FromArgb(70, 150, 230), tfont); AddHearts(2); }
        else AddText(byUser ? T("好耶~", "Yay~") : T("啵!", "Pop!"), Color.FromArgb(70, 150, 230), tfont);
        toy.Hide();          // 破掉的泡泡先消失，隔一会儿再冒出新的
        bubbleWait = 45;
    }

    void Chase(Rectangle wa, float speed)
    {
        chasing = true;
        px += dir * speed * K;
        if (tick % 4 == 0) frame++;
        if (px < wa.Left) px = wa.Left;
        if (px > wa.Right - Width) px = wa.Right - Width;
    }

    void PlayJump(float up, float side)
    {
        state = St.Jump; vy = -up * K; vx = side; playJump = true;
    }

    void PlayStep(Rectangle wa)
    {
        chasing = false;
        if (toy == null) { state = St.Idle; timer = 40; return; }
        if (--playTimer <= 0) { state = St.Idle; timer = rand.Next(60, 140); return; }
        float me = px + Width / 2f;
        if (toy.Kind == 3 && toy.carried)   // 叼着飞盘送回来给你
        {
            float cd = Cursor.Position.X - me;
            dir = cd > 0 ? 1 : -1;
            if (Math.Abs(cd) > 3 * SZ) Chase(wa, 3f);
            else
            {
                toy.carried = false; toy.vy = -2f * K; toy.vx = dir * 1f * K;
                AddText(T("给你~", "Here~"), PinkCol, tfont); happy = 20;
            }
            return;
        }
        float d = toy.x + toy.Width / 2f - me;
        if (Math.Abs(d) > 1) dir = d > 0 ? 1 : -1;
        if (toy.held) return;   // 你拿着玩具的时候，它就盯着看
        float petTop = py + TOP * SZ, tb = toy.y + toy.Height;
        switch (toy.Kind)
        {
            case 2:   // 顶气球
                if (Math.Abs(d) > 2 * SZ) Chase(wa, 3f);
                if (Math.Abs(d) <= 4 * SZ && kickCd == 0)
                {
                    if (toy.vy > 0 && tb > petTop - 7 * SZ && tb < petTop - SZ) { PlayJump(10f, d / 18f); kickCd = 15; }
                    else if (toy.landed) { kickAnim = 8; kickCd = 22; toy.Bump(dir); }
                }
                break;
            case 3:   // 飞盘
                if (Math.Abs(d) > 3 * SZ) Chase(wa, 3.5f);
                else if (kickCd == 0 && tb < petTop && tb > petTop - 8 * SZ) { PlayJump(10f, d / 15f); kickCd = 20; }
                break;
            case 4:   // 戳泡泡：等泡泡飘到头顶上方，再跳起来戳
                if (bubbleWait > 0) break;
                if (Math.Abs(d) > 2 * SZ) Chase(wa, 2.5f);
                if (Math.Abs(d) <= 3 * SZ && kickCd == 0 && tb < petTop - SZ && tb > petTop - 8 * SZ) { PlayJump(10f, d / 18f); kickCd = 30; }
                break;
            case 5:   // 追激光点
                if (Math.Abs(d) > 2 * SZ) Chase(wa, 4f);
                else if (kickCd == 0 && rand.Next(5) == 0) { PlayJump(7f, dir * 2f * K); kickCd = 25; }
                break;
            default:  // 足球 / 毛线球
                {
                    float reach = toy.Width / 2f + 4 * SZ;
                    if (Math.Abs(d) > reach) Chase(wa, 3f);
                    else if (toy.NearGround(2 * SZ) && kickCd == 0) { kickAnim = 8; kickCd = 22; toy.Kick(dir); }
                }
                break;
        }
    }

    // 碰到玩具时的反应（顶气球、接飞盘、戳泡泡）
    void PlayContact()
    {
        if (toy == null || toy.held || drag) return;
        bool active = state == St.Play || ((state == St.Jump || state == St.Fall) && playJump);
        if (!active) return;
        RectangleF me = new RectangleF(px + SZ, py + (TOP - 1) * SZ, 10 * SZ, 7 * SZ);
        RectangleF tr = new RectangleF(toy.x, toy.y, toy.Width, toy.Height);
        if (!me.IntersectsWith(tr)) return;
        switch (toy.Kind)
        {
            case 2:
                if (toy.vy >= -1f * K && toy.y < py + TOP * SZ)
                {
                    toy.Bump(dir); bounceCount++;
                    AddText(bounceCount + "!", Color.FromArgb(230, 80, 100), zfont, false);
                    if (bounceCount % 5 == 0) AddHearts(2);
                }
                break;
            case 3:
                if (!toy.carried)
                {
                    toy.carried = true; toy.vx = 0; toy.vy = 0; happy = 25;
                    AddText(T("接住啦!", "Got it!"), PinkCol, tfont);
                }
                break;
            case 4:   // 只有跳起来的时候才戳得破（站着碰到不算）
                if ((state == St.Jump || state == St.Fall) && playJump) PopBubble(false);
                break;
        }
    }

    // ---------- 粒子 ----------
    void AddHearts(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Particle p = new Particle();
            p.X = rand.Next(SZ, COLS * SZ - 3 * SZ); p.Y = (TOP - 1) * SZ;
            p.VY = -1.2f * K; p.Life = 30 + rand.Next(15); p.Kind = 0;
            parts.Add(p);
        }
    }

    void AddStars(int s)
    {
        for (int i = 0; i < 4; i++)
        {
            Particle p = new Particle();
            p.X = s < 0 ? rand.Next(SZ, 3 * SZ) : COLS * SZ - rand.Next(2 * SZ, 4 * SZ);
            p.Y = rand.Next(2 * SZ, 10 * SZ);
            p.VX = -s * (0.8f + (float)rand.NextDouble()) * K;
            p.VY = ((float)rand.NextDouble() - 0.5f) * 2f * K;
            p.Life = 12 + rand.Next(8); p.Kind = 1; p.Col = Yellow;
            parts.Add(p);
        }
    }

    void AddSpark(float x, float y, Color c, int life)
    {
        Particle p = new Particle();
        p.X = x; p.Y = y; p.Col = c; p.Kind = 1; p.Life = life;
        p.VX = ((float)rand.NextDouble() - 0.5f) * 2f * K;
        p.VY = -(0.6f + (float)rand.NextDouble()) * K;
        parts.Add(p);
    }

    void AddText(string s, Color c, Font f) { AddText(s, c, f, true); }
    void AddText(string s, Color c, Font f, bool bubble)
    {
        Particle p = new Particle();
        float w;
        using (Graphics g = CreateGraphics()) w = g.MeasureString(s, f).Width;
        p.X = Math.Max(0, (COLS * SZ - w) / 2 + (bubble ? 0 : rand.Next(-SZ / 2, SZ / 2 + 1)));
        p.Y = (TOP - (bubble ? 3 : 2)) * SZ;
        p.VY = (bubble ? -0.35f : -0.6f) * K; p.Life = bubble ? 55 : 45; p.Kind = 2; p.Text = s; p.Col = c; p.F = f;
        p.Bubble = bubble;
        parts.Add(p);
    }

    void AddCode()
    {
        Particle p = new Particle();
        p.X = rand.Next(2 * SZ, 8 * SZ); p.Y = (TOP - 1) * SZ;
        p.VY = -0.9f * K; p.VX = ((float)rand.NextDouble() - 0.5f) * 0.6f * K;
        p.Life = 34; p.Kind = 2; p.F = zfont;
        p.Text = CodeBits[rand.Next(CodeBits.Length)];
        p.Col = CodeCols[rand.Next(CodeCols.Length)];
        parts.Add(p);
    }

    void AddBubble()
    {
        Particle p = new Particle();
        p.X = 5 * SZ; p.Y = (TOP + 2) * SZ;
        p.VY = -(0.8f + (float)rand.NextDouble() * 0.5f) * K;
        p.VX = ((float)rand.NextDouble() - 0.5f) * 1.4f * K;
        p.Life = 60; p.Kind = 3;
        parts.Add(p);
    }

    // ---------- 主循环 ----------
    void Step()
    {
        tick++;
        // 睡觉 / 洗澡时说拜拜：一起慢慢淡出
        if (quitT > 0)
        {
            quitT--;
            double op = quitT < 15 ? quitT / 15.0 : 1.0;
            Opacity = op;
            if (nest != null && !nest.IsDisposed && nest.Visible) { nest.Opacity = op; if (inNest) nest.Invalidate(); }
            if (backdrop != null && !backdrop.IsDisposed && backdrop.Visible) backdrop.Opacity = op * Look.BgAlpha / 100.0;
            if (light != null && !light.IsDisposed && light.Visible) light.Fade(op);
            if (bath != null && !bath.IsDisposed) bath.Opacity = op;
            if (quitT == 0) { timerCtl.Stop(); Close(); return; }
        }
        if (nest != null && !nest.IsDisposed && nest.Visible) nest.Step();
        if (backdrop != null && !backdrop.IsDisposed && backdrop.Visible)
        {
            SyncBackdrop();
            if (tick % 3 == 0) backdrop.Invalidate();
            if (Weather == 3 && tick >= nextThunder)   // 打雷：间隔随机，有时连闪两下
            {
                BackdropForm.FlashFrom = tick; BackdropForm.FlashTo = tick + 2 + rand.Next(3);
                bool dbl = rand.Next(4) == 0;
                nextThunder = tick + (dbl ? 12 + rand.Next(10) : rand.Next(360, 1800));
                if (!dbl && rand.Next(2) == 0 && state != St.Sleep && quitT == 0)
                    AddText(T("轰隆~", "Rumble~"), Color.FromArgb(110, 110, 160), tfont, false);
            }
            // 鼠标移到落地灯上时，背景才接收点击（其它地方照样穿透到桌面）
            bool overLamp = false;
            if (Look.Bg == 1 && (Look.RoomDecor & 8) != 0)
            {
                Rectangle lr = new Rectangle(backdrop.Left + 35 * SZ, backdrop.Top + (int)(8.5f * SZ), (int)(3.2f * SZ), (int)(11.5f * SZ));
                overLamp = lr.Contains(Cursor.Position);
            }
            backdrop.SetClickable(overLamp);
        }
        UpdateLight();
        if (Look.Weather == 0 && tick >= nextWeatherTick)   // 自动天气：每 6~12 分钟变一次
        {
            nextWeatherTick = tick + rand.Next(10800, 21600);
            int r = rand.Next(100);
            autoWeather = r < 60 ? 1 : (r < 82 ? 2 : (r < 94 ? 3 : 4));
        }
        if (toy != null && toy.IsDisposed) toy = null;
        if (toy != null && toy.Kind == 4 && bubbleWait > 0)
        {
            if (--bubbleWait == 0) { SpawnBubble(); toy.poppedByClick = false; toy.Show(); }
        }
        else if (toy != null)
        {
            int ev = toy.Step();
            if (ev == 1 && state == St.Play)
            {
                AddText(T("进球啦!", "GOAL!"), Color.FromArgb(240, 150, 30), tfont);
                state = St.Dance; timer = 90; frame = 0; AddHearts(2);
            }
            if (ev == 2 && state == St.Play)
            {
                AddText(bounceCount >= 3 ? T("哎呀~ 顶了", "Oops~ ") + bounceCount + T("下", "") : T("哎呀~", "Oops~"), Color.FromArgb(230, 80, 100), tfont);
                bounceCount = 0;
            }
            if (ev == 3 || (toy.Kind == 4 && toy.poppedByClick))
            {
                if (toy.poppedByClick) { toy.poppedByClick = false; PopBubble(true); }
                else SpawnBubble();
            }
            if (toy.Kind == 3 && toy.carried)   // 飞盘叼在嘴边
            {
                if (state != St.Play && state != St.Jump && state != St.Fall) toy.carried = false;
                else
                {
                    toy.x = px + Width / 2f + dir * 3 * SZ - toy.Width / 2f;
                    toy.y = py + (TOP + 2) * SZ - toy.Height / 2f;
                    toy.Sync();
                }
            }
            PlayContact();
        }

        // 食物特效
        if (seedT > 0)
        {
            seedT--;
            if (seedT % 7 == 0)
            {
                Particle p = new Particle();
                p.Kind = 4; p.X = 6 * SZ; p.Y = (TOP + 3) * SZ; p.Life = 40;
                p.VX = (rand.Next(2) == 0 ? -1 : 1) * (2f + (float)rand.NextDouble()) * K; p.VY = -1.5f * K;
                parts.Add(p);
            }
        }
        if (bloat > 0) bloat--;
        if (milkT > 0) milkT--;
        if (sourT > 0) sourT--;
        if (shiver > 0) { shiver--; if (tick % 20 == 0) AddText("brr", Color.FromArgb(90, 160, 230), zfont, false); }
        if (coolT > 0)
        {
            coolT--;
            if (tick % 4 == 0)
            {
                Particle p = new Particle();
                p.Kind = 1; p.X = 6 * SZ + dir * 3 * SZ; p.Y = (TOP + 3) * SZ; p.Life = 18;
                p.VX = dir * (1.2f + (float)rand.NextDouble()) * K; p.VY = -0.3f * K; p.Col = Color.FromArgb(170, 235, 225);
                parts.Add(p);
            }
        }
        // 摸头：鼠标停在头上左右来回晃，Claude 会很开心
        if (CanPet())
        {
            Point c = Cursor.Position;
            float wx = c.X - px, wy = c.Y - py, cx = wx, cy = wy, W = COLS * SZ;
            // 贴在墙上时身体是横过来画的：把鼠标换算回“站着”的坐标
            int cs = state == St.Cling ? side : 0;
            if (cs < 0) { cx = wy; cy = W - wx; }
            else if (cs > 0) { cx = W - wy; cy = wx; }
            cy -= SeatDy();
            if (inNest && state == St.Nest) cy += 3 * SZ / 2;   // 窝里坐起来，头高一点
            bool onHead = cx >= 2 * SZ && cx < 10 * SZ && cy >= (TOP - 1) * SZ && cy < (TOP + 3) * SZ;
            // 只看鼠标自己的移动（换算到头的横向），Claude 自己在墙上爬动不算摸
            int raw = cs == 0 ? c.X : (cs < 0 ? c.Y : -c.Y);
            int sx = raw, mdx = sx - lastPetX;
            if (onHead && Math.Abs(mdx) >= 2)
            {
                int s = Math.Sign(mdx);
                if (s == -lastPetSign) petScore += 1f;   // 换方向一次 = 摸一下
                lastPetSign = s;
            }
            if (!onHead) lastPetSign = 0;
            lastPetX = sx;
        }
        else petScore = 0;
        petScore = Math.Max(0f, petScore - 0.04f);
        if (petScore >= 3f)
        {
            if (pettedT == 0 && tick - lastPetSay > 90) { AddText(T("嘿嘿~", "Hehe~"), PinkCol, tfont); lastPetSay = tick; }
            pettedT = 20;
        }
        if (pettedT > 0)
        {
            pettedT--; blushT = 90;
            if (happy < 3) happy = 3;
            if (tick % 12 == 0) AddHearts(1);
            // 摸头不会打断它正在做的事：边走边摸、边爬边摸都可以
        }
        if (pendingMeal != null && !drag && !inNest && !inBath && (state == St.Idle || state == St.Walk))
        {
            int[] m = pendingMeal; pendingMeal = null; ServeMeal(m);
        }
        if (blushT > 0) blushT--;
        if (tearT > 0) tearT--;
        if (scareT > 0) scareT--;

        if (rainbow > 0)
        {
            rainbow--;
            if (tick % 3 == 0)
            {
                Color[] rb = { Color.FromArgb(240, 80, 80), Color.FromArgb(250, 160, 50), Color.FromArgb(250, 220, 60), Color.FromArgb(90, 200, 110), Color.FromArgb(80, 150, 240), Color.FromArgb(170, 110, 230) };
                AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(2 * SZ, 10 * SZ), rb[rand.Next(rb.Length)], 16);
            }
        }
        if (bath != null && bath.IsDisposed) bath = null;
        if (bath != null) bath.Step();
        if (tick % 30 == 0 && ((nest != null && nest.Visible) || bath != null)) RaiseZ();   // 时不时把层级理一理

        // 每小时掷一次骰子：30% 概率准备一份礼物
        if (DateTime.Now >= nextGiftRoll)
        {
            nextGiftRoll = DateTime.Now.AddHours(1);
            if (rand.Next(100) < 30) giftPending = true;
        }
        if (giftPending && !drag && !inNest && !inBath && (state == St.Idle || state == St.Walk)) StartGift();

        Rectangle wa = Screen.FromPoint(new Point((int)(px + Width / 2), (int)(py + Height / 2))).WorkingArea;
        float ground = wa.Bottom - Height;
        float curDx = Cursor.Position.X - (px + Width / 2f);
        lookDir = curDx > Width ? 1 : (curDx < -Width ? -1 : 0);

        if (boost > 0) boost--;
        if (hot > 0) { hot--; if (tick % 4 == 0) AddSpark(rand.Next(2 * SZ, 9 * SZ), (TOP - 1) * SZ, tick % 8 == 0 ? Yellow : Color.FromArgb(255, 120, 40), 12); }
        if (shiny > 0) { shiny--; if (tick % 5 == 0) AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(4 * SZ, 11 * SZ), tick % 10 == 0 ? Color.White : Color.FromArgb(150, 210, 255), 14); }
        if (shake > 0) shake--;
        if (kickAnim > 0) kickAnim--;
        if (kickCd > 0) kickCd--;
        if (tick % 900 == 0 && full > 0) full--;

        if (!drag)
        {
            if (inNest)
            {
                if (nest == null || nest.IsDisposed || !nest.Visible) inNest = false;
                else
                {
                    px = NestPetX(); py = nest.y;
                    if (tick % 15 == 0) frame++;
                    if (state == St.Nest && quitT == 0) NestStep();
                    if (tick % 6 == 0 || state == St.Nest) nest.Invalidate();   // 在窝里也会慢慢呼吸
                }
            }
            if (inBath)
            {
                if (bath == null) inBath = false;
                else { px = bath.x + BathForm.PetCol * SZ; py = bath.y + BathForm.PetRow * SZ; }
            }
            if (!inNest && !inBath) Behave(wa, ground);
        }

        if (squash > 0) squash--;
        if (happy > 0) happy--;
        if (blink > 0) blink--; else if (rand.Next(110) == 0) blink = 5;

        for (int i = parts.Count - 1; i >= 0; i--)
        {
            Particle p = parts[i];
            if (p.Kind == 4 || p.Kind == 5) p.VY += 0.25f * K;   // 西瓜籽、爆米花会往下掉
            p.X += p.VX; p.Y += p.VY; p.Life--;
            if (p.Life <= 0 || p.Y < -SZ) parts.RemoveAt(i);
        }

        if (IsDisposed) return;
        Location = new Point((int)px, (int)py);
        Invalidate();
    }

    void Behave(Rectangle wa, float ground)
    {
        if (state != St.Idle) idleAct = 0;
        if (state != St.Cling || vdir != 0) clingAct = 0;
        if (follow && (state == St.Walk || state == St.Idle))
        {
            float curDx = Cursor.Position.X - (px + Width / 2f);
            if (Math.Abs(curDx) > Width * 0.7f) { state = St.Walk; dir = curDx > 0 ? 1 : -1; timer = Math.Max(timer, 5); }
            else if (state == St.Walk) { state = St.Idle; timer = 40; }
        }

        switch (state)
        {
            case St.Walk:
                px += dir * 2f * K * (boost > 0 ? 2.5f : 1f);
                if (tick % (boost > 0 ? 2 : 4) == 0) frame++;
                if (px < wa.Left || px > wa.Right - Width)
                {
                    int hit = px < wa.Left ? -1 : 1;
                    px = hit < 0 ? wa.Left : wa.Right - Width;
                    if (!follow && boost == 0 && rand.Next(100) < 35)   // 走到墙边，顺势爬上去
                    {
                        state = St.Cling; side = hit; vdir = -1; pinned = false;
                        timer = rand.Next(60, 160); squash = 5; break;
                    }
                    dir = -hit;
                }
                if (--timer <= 0) Pick();
                break;
            case St.Idle:
                if (idleAct > 0) IdleActStep();
                if (--timer <= 0) { idleAct = 0; Pick(); }
                break;
            case St.Sleep:   // 深度睡眠：不会自己醒
                if (tick % 15 == 0) frame++;
                break;
            case St.GoWater:   // 走到盆栽旁边
                if (!CanWater()) { state = St.Idle; timer = 40; break; }
                {
                    float tx = WaterX(), d = tx - px;
                    if (Math.Abs(d) <= 2.5f * K)
                    {
                        px = tx; dir = -1; state = St.Water; timer = 150;
                        AddText(T("浇浇水~", "Watering~"), Color.FromArgb(60, 150, 90), tfont);
                    }
                    else
                    {
                        dir = d > 0 ? 1 : -1; px += dir * 2f * K;
                        if (tick % 4 == 0) frame++;
                        if (--timer <= 0) { state = St.Idle; timer = 40; }
                    }
                }
                break;
            case St.Water:     // 拿喷壶浇水
                dir = -1;
                if (tick % 9 == 0) AddSpark(rand.Next(0, 2 * SZ), rand.Next(7 * SZ, 10 * SZ), Color.FromArgb(120, 210, 120), 14);
                if (--timer <= 0)
                {
                    state = St.Idle; timer = 50; happy = 25;
                    AddText(T("长高高~", "Grow big~"), Color.FromArgb(60, 150, 90), tfont);
                }
                break;
            case St.GoNest:
                if (!NestUsable()) { if (nestGoAwake) { state = St.Idle; timer = 40; } else { state = St.Sleep; frame = 0; } break; }
                if (nest.drag || nest.y < nest.Ground() - 1) break;   // 窝还在空中，等一下
                {
                    float tx = NestPetX(), d = tx - px;
                    if (Math.Abs(d) <= 3f * K) { px = tx; state = St.Jump; vy = -8f * K; vx = 0; intoNest = true; }
                    else
                    {
                        dir = d > 0 ? 1 : -1; px += dir * 2f * K;
                        if (tick % 6 == 0) frame++;
                        if (--timer <= 0) { if (nestGoAwake) { state = St.Idle; timer = 40; } else { state = St.Sleep; frame = 0; } }
                    }
                }
                break;
            case St.Gift:
                timer--;
                if (giftPhase == 0)
                {
                    if (tick % 110 == 0) AddText(T("有礼物给你!", "Gift for you!"), PinkCol, tfont);
                    if (timer <= 0) OpenGift();
                }
                else
                {
                    if (giftAnim > 0) GiftAnimStep();
                    if (!giftDone && giftAnim == 0 && timer % 180 == 90) AddText(T("点点它~", "Tap it~"), PinkCol, tfont);
                    if (timer <= 0 && giftAnim == 0) { state = St.Idle; timer = 40; }
                }
                break;
            case St.Meal:   // 坐着吃套餐，一样一样从餐盘上拿起来吃
                if (mealPos >= meal.Count) { EndMeal(); break; }
                if (EatStep())
                {
                    MealFx(foodIdx);
                    mealPos++;
                    if (mealPos >= meal.Count) EndMeal(); else NextMealFood();
                }
                break;
            case St.Play:
                PlayStep(wa);
                break;
            case St.Frozen:   // 被冻住：一动不动，飘雪花，过一会儿自己化开
                if (tick % 12 == 0) AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(2 * SZ, 6 * SZ), Color.White, 20);
                if (--frozenT <= 0) Thaw(false);
                break;
            case St.Computer:
                if (tick % 5 == 0) frame++;
                if (tick % 24 == 0) AddCode();
                if (--timer <= 0) { state = St.Idle; timer = 40; }
                break;
            case St.Dance:
                if (tick % 6 == 0) frame++;
                if (tick % 16 == 0) AddText(rand.Next(2) == 0 ? "♪" : "♫", CodeCols[rand.Next(CodeCols.Length)], zfont, false);
                if (--timer <= 0) { state = St.Idle; timer = 40; }
                break;
            case St.Bubble:
                if (tick % 20 == 0) AddBubble();
                if (--timer <= 0) { state = St.Idle; timer = 40; }
                break;
            case St.Stretch:
                if (--timer <= 0) { state = St.Idle; timer = 50; }
                break;
            case St.Eat:
                if (EatStep()) FinishEat();
                break;
            case St.Bye:
                if (tick % 5 == 0) frame++;
                timer--;
                if (timer < 12)
                {
                    Opacity = Math.Max(0.0, timer / 12.0);
                    if (nest != null && !nest.IsDisposed && nest.Visible) nest.Opacity = Opacity;
                    if (backdrop != null && !backdrop.IsDisposed && backdrop.Visible) backdrop.Opacity = Opacity * Look.BgAlpha / 100.0;
                    if (light != null && !light.IsDisposed && light.Visible) light.Fade(Opacity);
                }
                if (timer <= 0) { timerCtl.Stop(); Close(); }
                return;
            case St.Cling:
                px = side < 0 ? wa.Left : wa.Right - Width;
                if (vdir != 0)
                {
                    py += vdir * 1.5f * K;
                    if (tick % 6 == 0) frame++;
                    if (py < wa.Top) { py = wa.Top; vdir = 1; }
                    if (py > ground) { py = ground; vdir = -1; }
                }
                if (py > ground) py = ground;
                if (clingAct > 0) ClingActStep();
                if (--timer <= 0) PickCling();
                break;
            default:   // Fall / Jump
                vy += 1.0f * K; py += vy; px += vx;
                if (px < wa.Left) { px = wa.Left; vx = 0; }
                if (px > wa.Right - Width) { px = wa.Right - Width; vx = 0; }
                if (py >= ground)
                {
                    py = ground; vy = 0; vx = 0; squash = 5;
                    state = St.Idle; timer = 30;
                    if (hiccups > 0)   // 爆米花：一蹦一蹦打嗝
                    {
                        hiccups--; state = St.Jump; vy = -6f * K; PopcornBurst();
                        if (hiccups % 2 == 1) AddText(T("嗝!", "Hic!"), Color.FromArgb(200, 150, 60), tfont);
                    }
                    else if (playJump)   // 玩游戏时跳起来，落地后接着玩
                    {
                        playJump = false;
                        if (toy != null)
                        {
                            state = St.Play;
                            if (toy.Kind == 5 && Math.Abs(toy.x + toy.Width / 2f - (px + Width / 2f)) < 3 * SZ && rand.Next(2) == 0)
                                AddText(T("抓到啦!", "Got it!"), PinkCol, tfont);
                        }
                    }
                    if (intoNest)
                    {
                        intoNest = false;
                        if (NestUsable() && Math.Abs(NestPetX() - px) < SZ)
                        {
                            inNest = true; state = St.Sleep; frame = 0; squash = 0;
                            if (nestGoAwake) StartNestAct(0); else nestAct = 0;
                            nestGoAwake = false;
                            nest.Invalidate();
                        }
                    }
                    if (intoBath)
                    {
                        intoBath = false;
                        if (bath != null && !bath.IsDisposed)
                        {
                            inBath = true; state = St.Bath; squash = 0;
                            px = bath.x + BathForm.PetCol * SZ; py = bath.y + BathForm.PetRow * SZ;
                            bath.Invalidate();
                        }
                    }
                }
                break;
        }
        if (OnGround() && !inNest)
        {
            if (py < ground - 1) { state = St.Fall; vy = 0; vx = 0; }
            else if (py > ground) py = ground;
        }
    }

    // ---------- 鼠标 ----------
    static bool IsOuterEdge(Screen scr, int s, int y)
    {
        // 两块屏幕中间的那条边不吸附
        Rectangle b = scr.Bounds;
        Point probe = new Point(s < 0 ? b.Left - 20 : b.Right + 20, y);
        foreach (Screen o in Screen.AllScreens)
            if (o.Bounds.Contains(probe)) return false;
        return true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || state == St.Bye || quitT > 0 || inBath || intoBath) return;
        if (state == St.Frozen) { Poke(); return; }   // 冻住时点它 = 敲冰
        if (inNest) { NestClick(); return; }
        if (state == St.Gift && giftPhase == 1 && e.Y < TOP * SZ) { PlayGift(); return; }   // 点到头顶的礼物
        Point c = Cursor.Position;
        drag = true; dragMoved = false;
        snap = state == St.Cling ? side : 0;
        offX = c.X - px; offY = c.Y - py; down = c;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!drag) return;
        Point c = Cursor.Position;
        if (!dragMoved && Math.Abs(c.X - down.X) + Math.Abs(c.Y - down.Y) > 4)
        {
            dragMoved = true; intoNest = false; playJump = false; hiccups = 0;
            if (state == St.Gift && giftPhase == 0) giftPending = true;
            if (state != St.Cling) state = St.Fall;
        }
        if (!dragMoved) return;

        float rawX = c.X - offX, rawY = c.Y - offY;
        Screen scr = Screen.FromPoint(c);
        Rectangle wa = scr.WorkingArea;
        float thr = 30f * K;
        int ns = 0;
        if (rawX <= wa.Left + thr && IsOuterEdge(scr, -1, c.Y)) ns = -1;
        else if (rawX >= wa.Right - Width - thr && IsOuterEdge(scr, 1, c.Y)) ns = 1;
        if (ns != 0 && ns != snap) { squash = 5; AddStars(ns); }   // “啪”地吸上去
        snap = ns;

        if (snap != 0)
        {
            px = snap < 0 ? wa.Left : wa.Right - Width;
            py = Math.Max(wa.Top, Math.Min(wa.Bottom - Height, rawY));
        }
        else { px = rawX; py = rawY; }
        Location = new Point((int)px, (int)py);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !drag) return;
        drag = false;
        if (!dragMoved) Poke();
        else if (snap != 0) { state = St.Cling; side = snap; vdir = 0; timer = 50; pinned = true; }
        else { state = St.Fall; vy = 0; vx = 0; side = 0; }
        snap = 0;
    }

    // ---------- 绘制 ----------
    void R(Graphics g, Brush br, int x, int y, int w, int h)
    {
        x += ox; y += oy;
        int W = COLS * SZ;
        if (rot == 0) g.FillRectangle(br, x, y, w, h);
        else if (rot < 0) g.FillRectangle(br, W - (y + h), x, h, w);   // 脚朝左墙
        else g.FillRectangle(br, y, W - (x + w), h, w);                // 脚朝右墙
    }
    void Cell(Graphics g, Brush br, int c, int r, int w, int h) { R(g, br, c * SZ, r * SZ, w * SZ, h * SZ); }

    protected override void OnPaint(PaintEventArgs e) { Draw(e.Graphics); }

    void Draw(Graphics g)
    {
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        int cs = drag ? snap : ((state == St.Cling || state == St.Bye) ? side : 0);
        rot = cs;
        if (!inNest && !inBath)   // 在窝里 / 浴缸里时，身体由那边来画
        {
            if (state == St.Computer && !drag) DrawComputer(g); else DrawBody(g, cs);
        }
        rot = 0; ox = 0; oy = 0;

        if ((state == St.Eat || state == St.Meal) && !drag)
        {
            if (state == St.Meal) DrawTray(g);
            if (state == St.Eat || mealPos < meal.Count) DrawFood(g);
        }
        if (state == St.Gift && giftPhase == 1 && !drag) DrawGiftItem(g);

        if (state == St.Sleep && !drag)   // 像素 Z，一个比一个大，轻轻上下飘
        {
            int u0 = Math.Max(1, SZ / 5);
            int zx = 8 * SZ;
            for (int i = 0; i < 3; i++)
            {
                if (frame % 4 < i) continue;
                int u = u0 + i;
                int zy = (TOP - 1 - i) * SZ - (int)Math.Round((1 + Math.Sin(TAU * ((tick / 8 + i * 2) % 5) / 5)) * u0);
                PixelZ(g, C(150, 150, 205), zx, zy, u);
                zx += 4 * u + u0;
            }
        }

        int p3 = Math.Max(2, (int)Math.Round(3 * K));
        int hp = Math.Max(2, (int)Math.Round(2 * K));
        foreach (Particle p in parts)
        {
            int x = (int)p.X, y = (int)p.Y;
            if (p.Kind == 0)   // 带高光的像素爱心
            {
                string[] heart = { ".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..." };
                for (int r = 0; r < heart.Length; r++)
                    for (int c = 0; c < 7; c++)
                    {
                        char ch = heart[r][c];
                        if (ch == '.') continue;
                        g.FillRectangle(ch == 'W' ? Brushes.White : (ch == 'D' ? C(205, 60, 95) : pink), x + c * hp, y + r * hp, hp, hp);
                    }
            }
            else if (p.Kind == 1)   // 一闪一闪的小星星
            {
                using (Brush b = new SolidBrush(p.Col))
                {
                    g.FillRectangle(b, x + p3, y, p3, 3 * p3);
                    g.FillRectangle(b, x, y + p3, 3 * p3, p3);
                    if ((p.Life / 3) % 2 == 0)
                    {
                        g.FillRectangle(b, x + p3, y - p3, p3, p3);
                        g.FillRectangle(b, x + p3, y + 3 * p3, p3, p3);
                        g.FillRectangle(b, x - p3, y + p3, p3, p3);
                        g.FillRectangle(b, x + 3 * p3, y + p3, p3, p3);
                    }
                    g.FillRectangle(Brushes.White, x + p3, y + p3, p3, p3);
                }
            }
            else if (p.Kind == 2 && p.Bubble) DrawBubble(g, p);
            else if (p.Kind == 4) g.FillRectangle(dark, x, y, Math.Max(2, SZ / 4), Math.Max(3, SZ * 3 / 8));   // 西瓜籽
            else if (p.Kind == 5) { int s = Math.Max(2, SZ * 3 / 8); using (Brush pb = new SolidBrush(p.Col)) g.FillRectangle(pb, x, y, s, s); }   // 爆米花
            else if (p.Kind == 3)
            {
                g.FillRectangle(bubbleBr, x + p3, y, 2 * p3, p3);
                g.FillRectangle(bubbleBr, x + p3, y + 3 * p3, 2 * p3, p3);
                g.FillRectangle(bubbleBr, x, y + p3, p3, 2 * p3);
                g.FillRectangle(bubbleBr, x + 3 * p3, y + p3, p3, 2 * p3);
                g.FillRectangle(Brushes.White, x + 2 * p3, y + p3, p3, p3);
            }
            else
            {
                using (Brush b = new SolidBrush(p.Col)) g.DrawString(p.Text, p.F ?? zfont, b, p.X, p.Y);
            }
        }
    }

    // 任意行数的小像素图（字符对应 Pal 里的颜色）
    static void DrawRows(Graphics g, int x, int y, int fp, params string[] rows)
    {
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
            {
                char ch = rows[r][c];
                if (ch != '.') g.FillRectangle(Pal[ch], x + c * fp, y + r * fp, fp, fp);
            }
    }
    // 8x8 像素图拉伸到 w x h（牛奶盒被吸扁）
    static void DrawArtScaled(Graphics g, string[] rows, int x, int y, int w, int h)
    {
        for (int r = 0; r < 8; r++)
        {
            int y0 = y + r * h / 8, y1 = y + (r + 1) * h / 8;
            for (int c = 0; c < 8; c++)
            {
                char ch = rows[r][c];
                int x0 = x + c * w / 8, x1 = x + (c + 1) * w / 8;
                if (ch != '.' && x1 > x0 && y1 > y0) g.FillRectangle(Pal[ch], x0, y0, x1 - x0, y1 - y0);
            }
        }
    }

    // 正在吃的那一份食物，每种吃法画法不一样
    void DrawFood(Graphics g)
    {
        int fp = FoodPx(), half = SZ / 2;
        float tx, ty; FoodTarget(out tx, out ty);
        float e = 1 - (1 - foodT) * (1 - foodT);
        int x = (int)Math.Round(foodFromX + (tx - foodFromX) * e), y = (int)Math.Round(foodFromY + (ty - foodFromY) * e);
        string[] art = Foods[foodIdx];
        if (foodT < 1) { DrawArt(g, art, x, y, fp, 0); return; }
        int gap = BiteGap(), ph = eatTick % gap, k5 = Math.Min(4, ph * 5 / gap);
        bool done = bites >= BitesNeeded();
        switch (FoodStyle(foodIdx))
        {
            case 1:   // 端起杯子一口一口喝：热气慢慢没了，最后杯子空了
                {
                    string[] cup = (string[])art.Clone();
                    if (bites >= 3) cup[3] = cup[3].Replace('n', 'w');
                    int ang = done ? 0 : new[] { 0, -15, -30, -30, -15 }[k5];
                    GraphicsState gs = g.Save();
                    g.TranslateTransform(x, y + 2 * fp);
                    g.RotateTransform(ang);
                    DrawArt(g, cup, 0, -2 * fp, fp, Math.Min(2, bites));
                    g.Restore(gs);
                }
                break;
            case 2:   // 牛奶盒插着吸管，越吸越扁
                {
                    int w = 8 * fp - bites * 8 * fp / 10, hgt = 8 * fp - (ph < gap / 2 ? 0 : fp / 2);
                    int ax = x + (8 * fp - w) / 2, ay = y + 8 * fp - hgt;
                    DrawArtScaled(g, art, ax, ay, w, hgt);
                    int sq = Math.Max(2, SZ / 4);
                    float x0 = 6 * SZ, y0 = MouthY() + sq / 2, x1 = ax + w / 2f, y1 = ay - sq;
                    int n = Math.Max(3, (int)(Math.Abs(x1 - x0) + Math.Abs(y1 - y0)) / sq);
                    for (int i = 0; i <= n; i++)
                    {
                        float t = i / (float)n;
                        g.FillRectangle((i / 2) % 2 == 0 ? Brushes.White : accRed, (int)(x0 + (x1 - x0) * t), (int)(y0 + (y1 - y0) * t), sq, sq);
                    }
                }
                break;
            case 3:   // 伸舌头舔，越舔越小
                {
                    bool lick = ph < gap / 2 && !done;
                    DrawArt(g, art, x - (lick ? fp : 0), y, fp, Math.Min(4, bites));
                }
                break;
            case 4:   // 爆米花桶放在前面，一颗颗抛进嘴里
                DrawArt(g, art, x, y, fp, Math.Min(3, bites / 2));
                break;
            case 6:   // 面条：筷子从碗里夹起面，一口一口嗦上去
                {
                    DrawArt(g, art, x, y, fp, bites >= 3 ? 1 : 0);
                    int u = Math.Max(1, SZ / 4), my = MouthY() + half / 2, bowlTop = y + 2 * fp;
                    float k = done ? 1 : Math.Min(1f, ph / (gap * 0.7f));   // 这一口嗦了多少
                    int bot = (int)(bowlTop - (bowlTop - my) * k);
                    if (!done && bot > my)
                        using (Brush nb = new SolidBrush(Color.FromArgb(250, 215, 120)))
                        {
                            g.FillRectangle(nb, 6 * SZ - u, my, u, bot - my);
                            g.FillRectangle(nb, 6 * SZ + u / 2, my, u, Math.Max(u, bot - my - u));
                        }
                    // 两根筷子，斜着从右手伸过来
                    int tipY = done ? my + SZ : (int)(bowlTop + fp - (bowlTop + fp - (my + half)) * k);
                    using (Brush cb = new SolidBrush(Color.FromArgb(150, 100, 60)))
                        for (int s = 0; s < 2; s++)
                        {
                            float x0 = 9 * SZ + s * u * 2, y0 = my - SZ + s * u, x1 = 6 * SZ + 2 * u + s * u * 2, y1 = tipY + s * u;
                            int n = 10;
                            for (int i = 0; i <= n; i++)
                                g.FillRectangle(cb, (int)(x0 + (x1 - x0) * i / n), (int)(y0 + (y1 - y0) * i / n), u, u);
                        }
                }
                break;
            case 7:   // 薯条 / 葡萄：从盒子里拿一根（一颗）送进嘴里
                {
                    int left = BitesNeeded() - bites;
                    DrawArt(g, art, x, y, fp, foodIdx == FRIES ? Math.Min(3, (bites + 1) / 2) : Math.Min(6, bites));
                    if (!done && ph < gap * 0.6f)
                    {
                        float k = ph / (gap * 0.6f);
                        int u = Math.Max(1, SZ / 4);
                        int sx = (int)(x + 4 * fp), sy = (int)(y + (y - MouthY()) * 0 + fp);
                        int cx = (int)(sx + (6 * SZ - sx) * k), cy = (int)(sy + (MouthY() - sy) * k);
                        if (foodIdx == FRIES) g.FillRectangle(Pal[(char)104], cx, cy - 2 * u, u, 3 * u);   // 一根薯条
                        else g.FillRectangle(Pal[(char)86], cx - u / 2, cy - u / 2, 2 * u, 2 * u);         // 一颗葡萄
                    }
                }
                break;
            case 8:   // 龙虾：竖着慢慢整只吞进嘴里
                {
                    int lf = Math.Max(2, SZ / 2);
                    float k = Math.Min(1f, eatTick / (float)(BitesNeeded() * gap - 6));
                    int mouthBot = MouthY() + half;
                    int lx = 6 * SZ - 4 * lf, ly = (int)(mouthBot - 8 * lf + 8 * lf * k);
                    GraphicsState gs = g.Save();
                    g.SetClip(new Rectangle(0, 0, COLS * SZ, mouthBot));   // 嘴巴下面的部分已经吞进去了
                    DrawArt(g, art, lx, ly, lf, 0);
                    g.Restore(gs);
                }
                break;
            case 5:   // 薄荷糖：一下丢进嘴里含着
                if (eatTick < 8)
                {
                    float t = eatTick / 8f;
                    DrawArt(g, art, (int)(x + (6 * SZ - 4 * fp - x) * t), (int)(y + (MouthY() - 4 * fp - y) * t), fp, 0);
                }
                break;
            default:   // 一口一口咬掉
                DrawArt(g, art, x, y, fp, Math.Min(8, bites * 2));
                break;
        }
    }

    // 套餐的餐盘：放在身前，还没吃的摆在上面
    void DrawTray(Graphics g)
    {
        int fp = FoodPx(), half = SZ / 2, q = Math.Max(1, SZ / 4), ty = TrayY();
        Brush wood = C(170, 120, 80), rim = C(130, 88, 58);
        g.FillRectangle(wood, SZ, ty, 10 * SZ, half);
        g.FillRectangle(rim, SZ - q, ty - q, q, half + q);
        g.FillRectangle(rim, 11 * SZ, ty - q, q, half + q);
        int r = meal.Count - mealPos - 1;
        for (int i = mealPos + 1; i < meal.Count; i++) DrawArt(g, Foods[meal[i]], TraySlotX(i - mealPos - 1, r), ty - 8 * fp, fp, 0);
    }

    static readonly string[] CandyFlare = { "........", "B......B", "BB.PP.BB", "BBPwPPBB", "BBPPPPBB", "BB.PP.BB", "B......B", "........" };
    static readonly string[] CandyOpen = { "........", "BBBBBBBB", "BBPPPPBB", "BPPwPPPB", "BPPPPPPB", "BBPPPPBB", "BBBBBBBB", "........" };
    static readonly string[] CandyBare = { "........", "........", "..PPPP..", ".PPwPPP.", ".PPPPPP.", "..PPPP..", "........", "........" };

    // 举在头顶的礼物（可以点）
    void DrawGiftItem(Graphics g)
    {
        int fp = SZ / 2, bob = (tick / 8) % 2 == 0 ? 0 : SZ / 4;
        int x = (COLS * SZ - 8 * fp) / 2, y = (TOP - 4) * SZ - SZ / 2 - bob;
        string[] art = Gifts[giftIdx];
        switch (giftIdx)
        {
            case 0: case 1:   // 转圈圈，越转越慢
                if (giftAnim > 0)
                {
                    float t = 1 - giftAnim / 45f;
                    float ang = (float)(Math.Round(720 * (1 - (1 - t) * (1 - t)) / 30) * 30);
                    GraphicsState gs = g.Save();
                    g.TranslateTransform(x + 4 * fp, y + 4 * fp);
                    g.RotateTransform(ang);
                    DrawArt(g, art, -4 * fp, -4 * fp, fp, 0);
                    g.Restore(gs);
                    return;
                }
                break;
            case 2:   // 一大一小地闪
                if (giftAnim > 0 && (giftAnim / 4) % 2 == 0)
                {
                    int f2 = fp + Math.Max(1, fp / 2);
                    DrawArt(g, art, x + 4 * fp - 4 * f2, y + 4 * fp - 4 * f2, f2, 0);
                    return;
                }
                break;
            case 3:   // 情书：翻开封口，信纸慢慢抽出来
                if (giftDone)
                {
                    int rise = giftAnim > 0 ? Math.Min(3, Math.Max(0, (50 - giftAnim) / 8)) : 3;
                    y += 2 * fp;
                    DrawRows(g, x, y - 2 * fp, fp, "...qq...", ".qqqqqq.", "qqqqqqqq", "qqqqqqqq", "qqqqqqqq");
                    int pyT = y + (2 - rise) * fp;
                    g.FillRectangle(Pal['w'], x + fp / 2, pyT, 7 * fp, 6 * fp);
                    DrawRows(g, x + fp / 2 + fp, pyT + fp, fp, "rr.rr", "rrrrr", ".rrr.", "..r..");
                    DrawRows(g, x, y + 3 * fp, fp, "llllllll", "lqllllql", "llqllqll", "lllqqlll", "llllllll");
                    return;
                }
                break;
            case 4:   // 糖果：糖纸抖一抖 → 摊开 → 糖纸飞走
                if (giftDone)
                {
                    string[] a = CandyBare;
                    if (giftAnim > 25) a = (giftAnim / 3) % 2 == 0 ? CandyFlare : art;
                    else if (giftAnim > 15) a = CandyOpen;
                    DrawArt(g, a, x, y, fp, 0);
                    return;
                }
                break;
        }
        DrawArt(g, art, x, y, fp, 0);
    }

    // 4x4 的像素 Z
    static void PixelZ(Graphics g, Brush b, int x, int y, int u)
    {
        g.FillRectangle(b, x, y, 4 * u, u);
        g.FillRectangle(b, x + 2 * u, y + u, u, u);
        g.FillRectangle(b, x + u, y + 2 * u, u, u);
        g.FillRectangle(b, x, y + 3 * u, 4 * u, u);
    }

    // 像素风对话气泡：白底、深色描边、切角、底部小尾巴
    void DrawBubble(Graphics g, Particle p)
    {
        Font f = p.F ?? tfont;
        SizeF ts = g.MeasureString(p.Text, f);
        int u = Math.Max(1, (int)Math.Round(K));
        int tw = (int)Math.Ceiling(ts.Width), th = (int)Math.Ceiling(ts.Height);
        int pad = 2 * u, bw = tw + 2 * pad - u, bh = th + 2 * u;
        int W = COLS * SZ;
        int bx = Math.Max(u, Math.Min(W - bw - u, (int)p.X - pad));
        int by = (int)p.Y - u;
        Brush ol = C(80, 58, 52);
        g.FillRectangle(ol, bx + u, by, bw - 2 * u, bh);
        g.FillRectangle(ol, bx, by + u, bw, bh - 2 * u);
        g.FillRectangle(Brushes.White, bx + u, by + u, bw - 2 * u, bh - 2 * u);
        int cx = bx + bw / 2, t = 2 * u;   // 尾巴大一点
        g.FillRectangle(Brushes.White, cx - t, by + bh - u, 2 * t, u);
        g.FillRectangle(ol, cx - t - u, by + bh - u, u, t + u);
        g.FillRectangle(ol, cx + t, by + bh - u, u, t + u);
        g.FillRectangle(Brushes.White, cx - t, by + bh, 2 * t, t);
        g.FillRectangle(ol, cx - t, by + bh + t, u, u);
        g.FillRectangle(ol, cx + t - u, by + bh + t, u, u);
        g.FillRectangle(Brushes.White, cx - t + u, by + bh + t, 2 * t - 2 * u, u);
        g.FillRectangle(ol, cx - t + u, by + bh + t + u, 2 * t - 2 * u, u);
        using (Brush tb = new SolidBrush(p.Col))
            g.DrawString(p.Text, f, tb, bx + pad - u, by + 2 * u);
    }

    // 外套（B 为身体顶部，半格单位）
    void DrawCoat(Graphics g, int B)
    {
        accDx = 0;
        switch (outfit[3])
        {
            case 1: // 毛衣
                H(g, coatRed, 4, B + 6, 16, 4);
                H(g, coatRedDark, 4, B + 6, 16, 1);
                H(g, Brushes.White, 4, B + 8, 16, 1);
                break;
            case 2: // 雨衣
                H(g, coatYellow, 4, B + 6, 16, 4);
                H(g, coatYellowDark, 4, B + 6, 16, 1);
                H(g, accBrown, 12, B + 7, 1, 1); H(g, accBrown, 12, B + 9, 1, 1);
                break;
            case 3: // 西装
                H(g, coatNavy, 4, B + 6, 16, 4);
                H(g, Brushes.White, 10, B + 6, 4, 1); H(g, Brushes.White, 11, B + 7, 2, 1);
                H(g, coatNavyDark, 9, B + 6, 1, 2); H(g, coatNavyDark, 14, B + 6, 1, 2);
                H(g, accGold, 12, B + 9, 1, 1);
                break;
            case 4: // 卫衣
                H(g, coatGray, 4, B + 6, 16, 4);
                H(g, coatGrayDark, 8, B + 8, 8, 2);
                H(g, Brushes.White, 10, B + 6, 1, 2); H(g, Brushes.White, 13, B + 6, 1, 2);
                break;
            case 5: // 背带裤
                H(g, coatDenim, 8, B + 6, 8, 2);
                H(g, coatDenim, 4, B + 8, 16, 2);
                H(g, coatDenim, 5, B, 1, 7); H(g, coatDenim, 18, B, 1, 7);
                H(g, accGold, 8, B + 6, 1, 1); H(g, accGold, 15, B + 6, 1, 1);
                break;
            case 6: // 披风的扣子（披风本体画在身体后面）
                H(g, accGold, 6, B + 6, 1, 1); H(g, accGold, 17, B + 6, 1, 1);
                break;
            case 7: // 半穿风衣：左边穿好，右边从肩膀滑下来挂在胳膊肘
                {
                    Brush kh = C(200, 170, 110), khd = C(160, 128, 78);
                    H(g, kh, 4, B + 6, 8, 4);
                    H(g, kh, 4, B + 5, 2, 1);                 // 立起来的领子
                    H(g, khd, 11, B + 6, 1, 4);               // 前襟
                    H(g, khd, 9, B + 7, 1, 1);
                    H(g, khd, 4, B + 8, 8, 1);                // 腰带
                    H(g, khd, 11, B + 9, 1, 3);               // 垂下来的腰带头
                    H(g, kh, 14, B + 8, 8, 3);                // 滑落的右半边
                    H(g, khd, 14, B + 8, 8, 1);
                    H(g, kh, 16, B + 11, 5, 1);
                    H(g, khd, 21, B + 8, 1, 3);
                }
                break;
            case 8: // 羽绒服
                {
                    Brush p = C(240, 140, 170), pd = C(210, 105, 140);
                    H(g, p, 3, B + 6, 18, 4);
                    H(g, p, 8, B + 5, 8, 1);
                    H(g, pd, 3, B + 7, 18, 1);
                    H(g, pd, 3, B + 9, 18, 1);
                    H(g, C(230, 230, 238), 12, B + 5, 1, 5);  // 拉链
                }
                break;
            case 9: // 牛仔外套
                {
                    Brush d = C(80, 120, 190), dd = C(55, 90, 150);
                    H(g, d, 4, B + 6, 16, 4);
                    H(g, Brushes.White, 10, B + 6, 4, 4);
                    H(g, dd, 8, B + 6, 2, 2); H(g, dd, 14, B + 6, 2, 2);
                    H(g, dd, 5, B + 8, 3, 1); H(g, dd, 16, B + 8, 3, 1);
                    H(g, accGold, 9, B + 9, 1, 1); H(g, accGold, 14, B + 9, 1, 1);
                }
                break;
            case 10: // 水手服
                H(g, C(245, 245, 248), 4, B + 6, 16, 4);
                H(g, accNavy, 4, B + 6, 16, 1);
                H(g, accNavy, 8, B + 7, 2, 1); H(g, accNavy, 14, B + 7, 2, 1);
                H(g, accRed, 11, B + 7, 2, 2);
                H(g, accNavy, 4, B + 9, 16, 1);
                break;
        }
    }

    void DrawBody(Graphics g, int cs)
    {
        int half = SZ / 2;
        bool held = drag && dragMoved && cs == 0;
        bool climbing = cs != 0 && !drag && vdir != 0 && state == St.Cling;
        bool walking = (!drag && (state == St.Walk || state == St.GoNest || state == St.GoWater || (state == St.Play && chasing))) || climbing;
        bool watering = state == St.Water && !drag;
        bool dance = state == St.Dance && !drag;
        bool bubble = state == St.Bubble && !drag;
        bool stretch = state == St.Stretch && !drag && timer > 8 && timer < 38;
        bool sit = state == St.Meal && !drag;
        bool eat = (state == St.Eat || (sit && mealPos < meal.Count)) && !drag && foodT >= 1;
        bool bye = state == St.Bye || bathBye || nestBye;
        bool frozen = state == St.Frozen && !drag;
        bool giftHold = state == St.Gift && giftPhase == 0 && !drag;
        bool giftShow = state == St.Gift && giftPhase == 1 && !drag;
        bool kicking = kickAnim > 0 && !drag && state == St.Play;
        bool airborne = !drag && (state == St.Jump || state == St.Fall);
        bool rising = airborne && vy < -2f * K, falling = airborne && vy > 2.5f * K;
        int b = TOP, B = 2 * b;
        Pal3 body = frozen ? PalOf(Color.FromArgb(165, 210, 245)) : BodyPal();
        Pal3 armL = frozen ? body : (SleevePal(0) ?? body), armR = frozen ? body : (SleevePal(1) ?? body);
        accDx = 0;
        int ia = (state == St.Idle && !drag && cs == 0 && !nestDraw) ? idleAct : 0;     // 平时的小动作
        int ca = (state == St.Cling && !drag && vdir == 0) ? clingAct : 0;              // 墙上的待机
        bool sipping = ia == 3 && actT % 90 >= 62;                                       // 端起茶杯喝一口
        bool sneezed = ia == 7 && actT >= 72;
        bool chewing = ca == 6 && actT % 45 < 26;

        // ---- 8 帧动画：相位按时间算，所以速度和以前一样，只是中间多了过渡帧 ----
        int q = Math.Max(1, SZ / 4);
        int ph8 = climbing ? (tick * 5 / 12) % 5 : (boost > 0 ? (tick * 5 / 8) % 5 : (tick * 5 / 16) % 5);   // 走路 / 爬墙（5 帧）
        int d8 = (tick * 5 / 24) % 5;                                                              // 跳舞
        double stretchAmt = 0;
        if (state == St.Stretch && !drag)
        {
            int t = 46 - timer;
            stretchAmt = Math.Round((t < 8 ? t / 8.0 : (timer < 8 ? timer / 8.0 : 1.0)) * 4) / 4;
        }
        stretch = stretchAmt > 0.5;

        if (dance)
        {
            double s = Math.Sin(TAU * d8 / 5);
            ox = (int)Math.Round(s * half);
            oy = -(int)Math.Round(Math.Abs(s) * half);
        }
        if (giftHold) oy = -(int)Math.Round(Math.Max(0, Math.Sin(TAU * ((tick / 8) % 5) / 5)) * half);   // 捧着礼物一蹦一蹦
        if (shake > 0) ox = (int)Math.Round(Math.Sin(TAU * ((shake * 5 / 6) % 5) / 5) * half);
        if ((shiver > 0 || sourT > 0 || scareT > 0) && !dance && !drag) ox = (tick / 2) % 2 == 0 ? q : -q;   // 冷得 / 酸得发抖
        if (ia == 4)   // 原地蹦蹦跳跳：整个人（连腿）往上弹
        {
            double hh = Math.Sin(Math.PI * (actT % 18) / 18.0);
            oy -= (int)Math.Round(hh * 5 / 4.0 * SZ / q) * q;
        }
        if (ca == 5) ox = (int)Math.Round(Math.Sin(TAU * ((tick / 5) % 10) / 10) * half / q) * q;   // 哼着歌左右晃

        // 身体起伏（像素级）：呼吸、走路颠、落地压扁、起跳/伸懒腰拉长
        double bobF = 0, lift = 0;
        if (!drag && !airborne && !dance)
        {
            if (state == St.Idle || state == St.Eat || state == St.Gift || state == St.Bubble || state == St.Meal || (state == St.Cling && vdir == 0))
                bobF = (1 - Math.Cos(TAU * ((tick * 5 / 56) % 5) / 5)) / 2 * half;
            if (state == St.Sleep || ca == 3) bobF = (1 - Math.Cos(TAU * ((tick / 18) % 5) / 5)) / 2 * half;
            if (ia == 4) bobF = 0;
            if (walking) bobF = new[] { 2, 1, 0, 1, 0 }[ph8] * q / 2.0;
        }
        if (squash > 0) bobF = squash * SZ / 5.0;
        if (sit) bobF += SZ / 2;   // 坐下：身体低半格，腿露出一截
        if (rising) lift = Math.Round(Math.Min(half, -vy / (6f * K) * half) / q) * q;
        if (stretchAmt > 0) lift = stretchAmt * SZ;
        if (ia == 7 && actT < 72) lift = Math.Min(actT / 24, 2) * q;   // 憋喷嚏：身子慢慢往上仰
        int bobPx = (int)Math.Round(bobF - lift), legStretch = (int)Math.Round(lift);

        if (outfit[3] == 6) { oy += bobPx; H(g, coatCape, 2, B + 3, 20, 9); oy -= bobPx; }   // 披风在身后

        // 腿（不跟着身体起伏，身体压下来就像蹲了一下）
        int[] legs = { 2, 4, 7, 9 };
        int[] liftA = { 0, 2, 4, 0, 0 }, liftB = { 0, 0, 0, 4, 2 };
        int kickLeg = dir > 0 ? 3 : 0;
        for (int i = 0; i < 4; i++)   // 坐着时身体压低一格，腿露出短短一截
        {
            int lp = 0;
            if (walking) lp = (i % 2 == 0 ? liftA : liftB)[ph8] * q;
            if (held) lp = (int)Math.Round((1 + Math.Sin(TAU * ((tick / 2 + i) % 5) / 5)) / 2 * SZ);   // 被拎起来蹬腿
            if (kicking && i == kickLeg) lp = SZ;
            if (falling) lp = half;
            if (ca == 4) lp = ((tick / 7 + i) % 2 == 0) ? half : 0;                      // 贴着墙晃腿
            if (ia == 4) lp = (actT % 18 > 3 && actT % 18 < 15) ? q : 0;                 // 跳起来时脚尖收一下
            LegPx(g, body, 2 * legs[i], B + 10, lp, legStretch);
        }
        if (kicking)   // 踢出去的那只脚，8 帧伸出去再收回来
        {
            int ext = new[] { 0, 0, 2, 2, 4, 4, 2, 0, 0 }[kickAnim];
            ox += dir * ext * q;
            Block(g, body, 2 * legs[kickLeg], B + 10, 2, 2);
            ox -= dir * ext * q;
        }

        int oy0 = oy;
        oy += bobPx;
        if (bloat > 0) Block(g, body, 3, B, 18, 10); else Block(g, body, 4, B, 16, 10);   // 吃了汉堡胖一圈
        if (!frozen) DrawCoat(g, B);

        int la = B + 4, ra = B + 4, laPx = 0, raPx = 0;   // 手（半格 + 像素微调）
        if (held || falling) { la = B + 2; ra = B + 2; }
        if (rising) { la = B + 5; ra = B + 5; }
        if (stretch || giftShow) { la = B - 2; ra = B - 2; }
        if (watering) la = B;   // 左手举着喷壶
        if (dance) { int s = (int)Math.Round(Math.Sin(TAU * d8 / 5) * SZ); laPx = -s; raPx = s; }
        if (walking && !climbing) { int s = (int)Math.Round(Math.Sin(TAU * ph8 / 5) * q); laPx += s; raPx -= s; }   // 走路时手前后摆
        if (bye) { ra = B - 2; raPx = (int)Math.Round(Math.Sin(TAU * ((tick / 2) % 5) / 5) * half); }
        if (sleepyBye) { ra = B + 1; raPx = (int)Math.Round(Math.Sin(TAU * ((tick / 5) % 5) / 5) * q); }   // 迷迷糊糊慢慢挥手          // 举高小手挥一挥
        if (ia == 6 || ca == 2) { ra = B - 2; raPx = (int)Math.Round(Math.Sin(TAU * ((tick / 3) % 5) / 5) * half); }   // 打招呼
        int yoLen = 0;
        if (ia == 2)   // 溜溜球：手举在胸前，绳子一放一收
        {
            int ph = actT % 28;
            yoLen = ph < 14 ? ph / 2 : (28 - ph) / 2;
            ra = B + 2; raPx = yoLen == 0 ? -q : 0;
        }
        if (ia == 3) { ra = sipping ? B + 1 : B + 4; }                  // 端着茶杯，喝的时候举高
        if (ia == 4) { int s = (actT % 18 > 3 && actT % 18 < 15) ? 1 : 0; la = B + 3 - s; ra = B + 3 - s; }   // 跳起来小手扬起
        if (ia == 5) { la = B + 5; laPx = (int)Math.Round(Math.Sin(TAU * ((tick / 4) % 6) / 6) * q); }   // 扫帚
        if (ia == 1 && actT >= 110) { la = B - 2; ra = B - 2; }        // 想到了！举手
        if (ca == 6) ra = chewing ? B + 4 : B + 5;                       // 拿着饼干
        if (nestDraw && !nestBye && nestAct >= 1 && nestAct <= 5)   // 窝里抱着东西：两只方块小手扶在两边
        {
            la = B + 6; ra = B + 6; laPx = 0; raPx = 0;
            if (nestAct == 1 || nestAct == 4) { laPx = -((tick / 3) % 2) * q; raPx = -(((tick / 3) + 1) % 2) * q; }   // 敲键盘 / 按按钮
        }
        Block(g, armL, 0, la, 4, 4, laPx);
        Block(g, armR, 20, ra, 4, 4, raPx);

        int e1 = 3, e2 = 8, ld = 0;
        if ((state == St.Walk || state == St.GoNest || state == St.GoWater || state == St.Play) && !drag) ld = dir;
        if (watering) ld = -1;
        if (state == St.Idle && !drag && cs == 0 && shake == 0) ld = lookDir;   // 发呆时眼睛跟着鼠标
        if (climbing) ld = cs < 0 ? vdir : -vdir;
        if (ia == 1 && actT < 110) ld = (actT / 35) % 2 == 0 ? 1 : -1;   // 想事情：眼珠子转来转去
        if (ia == 2) ld = 1;
        if (ia == 5) ld = -1;
        if (ca == 1) ld = (actT / 30) % 3 == 0 ? 0 : ((actT / 30) % 3 == 1 ? 1 : -1);   // 东张西望
        if (ld > 0) { e1 = 4; e2 = 8; }
        if (ld < 0) { e1 = 3; e2 = 7; }
        double open = (blink >= 1 && blink <= 6) ? BlinkOpen[blink] : 1.0;   // 眨眼：半闭 → 闭上 → 半闭 → 睁开
        if (frozen) open = 1.0;
        bool closed = (nestDraw && nestAct == 6) || !held && !frozen && ((state == St.Sleep && !drag) || open <= 0 || happy > 0
            || dance || stretch || bye || shake > 0 || giftShow || sourT > 0);
        if (sleepyBye) { closed = false; open = 0.5; }   // 半睁着眼
        if (ia == 4 || ia == 6 || sipping || sneezed || ca == 2 || ca == 3 || ca == 5 || (ca == 6 && chewing)) closed = true;
        if (ia == 7 && actT < 72 && !closed) open = actT < 40 ? 0.5 : 0;   // 眯眼憋喷嚏
        if (ia == 7 && actT >= 40 && actT < 72) closed = true;
        int[] eyes = { e1, e2 };
        foreach (int ex in eyes)
        {
            if (closed) R(g, dark, ex * SZ, (b + 2) * SZ, SZ, half);
            else if (open < 1 && !held)
            {
                int eh = Math.Max(half, (int)Math.Round(2 * SZ * open));
                R(g, dark, ex * SZ, (b + 2) * SZ + half / 2 - eh / 2, SZ, eh);
            }
            else Cell(g, dark, ex, b + 1, 1, 2);
        }

        if (bubble) R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, SZ);                    // 嘟嘴
        if (stretch) R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, SZ);                   // 打哈欠
        if (ia == 7 && actT < 72) R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, actT < 40 ? half : SZ);   // 嘴巴越张越大
        if (sneezed && actT < 84) R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, SZ);                   // 阿嚏!
        if (sipping || ca == 5) R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, half);                      // 嘟嘴：喝茶 / 哼歌
        if (ca == 3) R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, q);                                    // 打盹的小嘴
        if (chewing)
        {
            int mh = half + (int)Math.Round((1 + Math.Sin(TAU * ((actT / 2) % 5) / 5)) / 2 * half);
            R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, mh);
        }
        if (ia == 1 && actT >= 110 || ia == 6 || ia == 4) R(g, dark, 5 * SZ, (b + 3) * SZ, 2 * SZ, half);   // 开心地笑
        if (eat)
        {
            int st = FoodStyle(foodIdx), gap = BiteGap(), ph = eatTick % gap;
            int mh = half + (int)Math.Round((1 + Math.Sin(TAU * ((eatTick / 2) % 5) / 5)) / 2 * half);
            switch (st)
            {
                case 1: case 2: case 6:   // 嘟着嘴喝 / 吸 / 嗦面
                    R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, half);
                    break;
                case 3:           // 伸舌头舔
                    if (ph < gap / 2 && bites < BitesNeeded())
                    {
                        R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, SZ);
                        R(g, C(240, 110, 135), 6 * SZ, (b + 3) * SZ, SZ + half, half);
                    }
                    else R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, q);
                    break;
                case 8:           // 张大嘴吞龙虾
                    R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, SZ + half);
                    break;
                case 7:           // 一根一根吃：张嘴接，再嚼
                    if (ph < gap * 0.6f) R(g, dark, 5 * SZ + half, (b + 3) * SZ - half, SZ, SZ);
                    else R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, mh);
                    break;
                case 4:           // 张大嘴接爆米花
                    if (ph < 8) R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, SZ + half);
                    else R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, mh);
                    break;
                case 5:           // 含着糖：一边腮帮子鼓起来，左右换
                    if (eatTick < 8) R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, SZ);
                    else
                    {
                        R(g, dark, 5 * SZ + half, (b + 3) * SZ, SZ, q);
                        H(g, C(140, 225, 195), bites % 2 == 0 ? 18 : 4, B + 5, 2, 2);   // 糖在这边腮帮子里
                    }
                    break;
                default:          // 嚼嚼嚼（5 帧）
                    R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, mh);
                    break;
            }
        }

        DrawAcc(g, B, B, B + 2, 0, eyes);
        accDx = 0;
        if (blushT > 0 && !frozen)   // 被摸得脸红红
        {
            H(g, accBlush, Math.Max(4, 2 * e1 - 2), B + 5, 2, 1);
            H(g, accBlush, Math.Min(18, 2 * e2 + 2), B + 5, 2, 1);
        }
        if (tearT > 0 && !frozen)   // 看悲伤的电影掉眼泪
        {
            Brush tb = C(120, 190, 255);
            int ty = B + 6 + (tick / 5) % 3;
            H(g, tb, 2 * e1, ty, 1, 1); H(g, tb, 2 * e2 + 1, ty, 1, 1);
        }
        if (watering)   // 绿色小喷壶 + 水滴
        {
            Brush can = C(90, 170, 120), canDark = C(60, 130, 90), drop = C(120, 190, 255);
            int tilt = (tick / 6) % 2;
            H(g, canDark, 1, B - 4, 3, 1);            // 提手
            H(g, can, 0, B - 3, 4, 3);                // 壶身
            H(g, can, 0, B - 2 - tilt, 1, 1);         // 壶嘴（一点一点往下倒）
            for (int k = 0; k < 3; k++)
                H(g, drop, (k % 2), B + ((tick / 2 + k * 3) % 9), 1, 1);
        }
        if (milkT > 0) { H(g, Brushes.White, 8, B + 6, 8, 1); H(g, Brushes.White, 9, B + 7, 1, 1); H(g, Brushes.White, 14, B + 7, 1, 1); }   // 牛奶胡子
        if (sourT > 0) PH(g, dark, 9, B + 6, "X.X.X", ".X.X.");   // 酸得嘴巴皱起来
        if (frozen)   // 冰块
        {
            Brush ice = C(200, 235, 255), crack = C(110, 170, 210);
            H(g, ice, 0, B - 4, 24, 1); H(g, ice, 0, B + 13, 24, 1);
            H(g, ice, 0, B - 4, 1, 18); H(g, ice, 23, B - 4, 1, 18);
            H(g, Brushes.White, 2, B - 3, 3, 1); H(g, Brushes.White, 2, B - 2, 1, 2); H(g, Brushes.White, 21, B + 10, 1, 2);
            if (iceHP < 3) PH(g, crack, 18, B - 3, "X..", ".X.", ".X.", "..X");
            if (iceHP < 2) PH(g, crack, 2, B + 8, "..X", ".X.", "X..", ".X.");
        }

        if (giftHold)   // 捧在身前的礼物盒
        {
            accDx = 0;
            int by2 = B + 6;
            H(g, giftRed, 8, by2, 8, 7);
            H(g, accGold, 11, by2, 2, 7);
            H(g, accGold, 8, by2 + 3, 8, 1);
            H(g, accGold, 9, by2 - 2, 2, 2); H(g, accGold, 13, by2 - 2, 2, 2);
            H(g, accGoldDark, 11, by2 - 1, 2, 1);
        }
        if (nestDraw && !nestBye) DrawNestProp(g);
        if (ia > 0 || ca > 0) DrawIdleProp(g, B, ia, ca, ra, raPx, la, laPx, yoLen, sipping);
        oy = oy0;
    }

    // 小动作用到的道具：都按“站着”的坐标画，贴墙时会跟着身体一起转过来
    void DrawIdleProp(Graphics g, int B, int ia, int ca, int ra, int raPx, int la, int laPx, int yoLen, bool sipping)
    {
        int half = SZ / 2, q = Math.Max(1, SZ / 4);
        accDx = 0;
        if (ia == 1)   // 想事情：头顶冒出 … 然后灯泡亮了
        {
            Brush dot = C(150, 150, 175);
            if (actT < 110)
            {
                int n = Math.Min(3, actT / 25);
                if (n >= 1) H(g, dot, 16, B - 2, 1, 1);
                if (n >= 2) H(g, dot, 18, B - 4, 2, 2);
                if (n >= 3) H(g, dot, 20, B - 7, 3, 3);
            }
            else
            {
                Brush bulb = (actT / 4) % 2 == 0 ? C(255, 225, 80) : C(255, 245, 160);
                PH(g, bulb, 9, B - 9, ".XXX.", "XXXXX", "XXXXX", ".XXX.");
                H(g, Brushes.White, 10, B - 8, 1, 1);
                H(g, C(150, 150, 165), 10, B - 5, 3, 1);
                H(g, C(110, 110, 125), 10, B - 4, 3, 1);
                Brush ray = C(255, 210, 60);
                if ((actT / 6) % 2 == 0) { H(g, ray, 7, B - 8, 1, 1); H(g, ray, 15, B - 8, 1, 1); H(g, ray, 11, B - 10, 1, 1); }
            }
        }
        else if (ia == 2)   // 溜溜球：从右手垂下来
        {
            int top = ra + 4;
            oy += raPx;
            R(g, C(240, 240, 245), 22 * half - q / 2, top * half, Math.Max(1, q / 2 + 1), yoLen * half);
            Brush yo = C(230, 70, 90);
            H(g, yo, 21, top + yoLen, 2, 2);
            R(g, C(255, 200, 210), 22 * half - q / 2, (top + yoLen) * half + q, Math.Max(1, q), Math.Max(1, q));
            oy -= raPx;
        }
        else if (ia == 3)   // 一杯冒热气的茶，放在右手上
        {
            int cy = ra - 3;
            Brush cup = C(245, 245, 248), tea = C(170, 105, 60), rim = C(200, 200, 210);
            H(g, cup, 19, cy, 4, 3); H(g, tea, 19, cy, 4, 1); H(g, rim, 19, cy + 2, 4, 1);
            H(g, rim, 23, cy + 1, 1, 1);   // 杯把
            if (!sipping)
            {
                Brush steam = C(225, 225, 235);
                int s = (tick / 8) % 2;
                H(g, steam, 20 + s, cy - 2, 1, 1); H(g, steam, 21 - s, cy - 3, 1, 1);
                H(g, steam, 22 - s, cy - 2, 1, 1); H(g, steam, 21 + s, cy - 4, 1, 1);
            }
        }
        else if (ia == 5)   // 扫帚：左手握着，扫帚头在地上左右扫
        {
            accDx = laPx;
            Brush stick = C(150, 100, 60), straw = C(225, 185, 90), band = C(190, 70, 60);
            PH(g, stick, 1, la + 1, "..X", ".X.", ".X.", ".X.", "X..", "X..", "X..");
            H(g, band, 0, B + 12, 3, 1);
            H(g, straw, 0, B + 13, 4, 1);
            PH(g, C(200, 160, 70), 0, B + 13, "X.X.");
            accDx = 0;
        }
        else if (ca == 6)   // 饼干：咬一口少一块
        {
            int bitesTaken = (actT / 45) % 4;
            if (bitesTaken < 3)
            {
                int cy = ra + 1;
                Brush ck = C(215, 160, 95), chip = C(90, 55, 35);
                if (bitesTaken == 0) PH(g, ck, 16, cy, ".XXX", "XXXX", "XXXX", ".XX.");
                else if (bitesTaken == 1) PH(g, ck, 16, cy, "..XX", ".XXX", "XXXX", ".XX.");   // 咬掉一口
                else PH(g, ck, 16, cy, "....", "..XX", ".XXX", "..X.");
                H(g, chip, 18, cy + 1, 1, 1); if (bitesTaken < 2) H(g, chip, 17, cy + 2, 1, 1);
            }
        }
    }

    // ---------- 像素明暗：底色 + 顶部高光 + 底部/右侧阴影 ----------
    class Pal3 { public Brush Base, Light, Dark; }
    static readonly Dictionary<int, Pal3> PalCache = new Dictionary<int, Pal3>();
    static Pal3 PalOf(Color c)
    {
        int key = c.ToArgb();
        Pal3 p;
        if (!PalCache.TryGetValue(key, out p))
        {
            p = new Pal3();
            p.Base = new SolidBrush(c);
            p.Light = new SolidBrush(NestForm.Mix(c, Color.White, 0.2f));
            p.Dark = new SolidBrush(NestForm.Mix(c, Color.Black, 0.16f));
            PalCache[key] = p;
        }
        return p;
    }
    Pal3 BodyPal() { return PalOf(hot > 0 ? Color.FromArgb(232, 62, 48) : BodyCol); }
    Pal3 SleevePal(int side)
    {
        Color c = SleeveColor(outfit[3], side);
        return c.IsEmpty ? null : PalOf(c);
    }
    void Block(Graphics g, Pal3 p, int hx, int hy, int w, int h)
    {
        H(g, p.Base, hx, hy, w, h);   // 纯色块，不加明暗
    }
    void Block(Graphics g, Pal3 p, int hx, int hy, int w, int h, int dyPx)
    {
        oy += dyPx; Block(g, p, hx, hy, w, h); oy -= dyPx;
    }
    // 一条腿：liftPx 抬脚多少像素，stretchPx 往上拉长多少像素
    void LegPx(Graphics g, Pal3 p, int hx, int hy, int liftPx, int stretchPx)
    {
        int hf = SZ / 2, q = Math.Max(1, SZ / 4);
        int top = hy * hf - stretchPx, h = Math.Max(q, 2 * SZ + stretchPx - liftPx);
        int x = hx * hf + accDx;
        R(g, p.Base, x, top, 2 * hf, h);   // 纯色块
    }
    const double TAU = Math.PI * 2;
    static readonly double[] BlinkOpen = { 1, 0.5, 0.5, 0, 0, 0.5, 0.5 };

    // 以半格为单位画配饰
    void H(Graphics g, Brush br, int hx, int hy, int w, int h)
    {
        int hf = SZ / 2;
        R(g, br, hx * hf + accDx, hy * hf, w * hf, h * hf);
    }

    // B: 身体顶部（半格）, HB: 头顶（伸懒腰时更高）, EY: 眼睛顶部, edx: 眼睛像素偏移
    void DrawAcc(Graphics g, int B, int HB, int EY, int edx, int[] eyes)
    {
        int e1 = eyes[0], e2 = eyes[1];
        accDx = 0;
        switch (outfit[2])
        {
            case 1: // 领结
                H(g, accBlack, 8, B + 7, 2, 3); H(g, accBlack, 10, B + 8, 1, 1);
                H(g, accGray, 11, B + 7, 2, 3);
                H(g, accBlack, 13, B + 8, 1, 1); H(g, accBlack, 14, B + 7, 2, 3);
                break;
            case 2: // 领带
                H(g, accNavy, 11, B + 6, 2, 1);
                H(g, accBlue, 11, B + 7, 2, 1); H(g, accBlue, 10, B + 8, 4, 2);
                H(g, accNavy, 11, B + 10, 2, 1);
                H(g, accGold, 11, B + 8, 1, 1); H(g, accGold, 12, B + 9, 1, 1);
                break;
            case 3: // 围巾
                H(g, accRed, 4, B + 6, 16, 2);
                for (int x = 6; x < 20; x += 4) H(g, Brushes.White, x, B + 6, 1, 2);
                H(g, accRed, 15, B + 8, 2, 2); H(g, Brushes.White, 16, B + 10, 1, 1);
                break;
            case 4: // 铃铛项圈
                H(g, accRed, 4, B + 7, 16, 1);
                H(g, accGold, 11, B + 8, 2, 1); H(g, accGold, 10, B + 9, 4, 1);
                H(g, accGoldDark, 11, B + 10, 2, 1);
                break;
            case 5: // 条纹围巾
                for (int x = 4; x < 20; x += 2) H(g, (x / 2) % 2 == 0 ? accBlue : Brushes.White, x, B + 6, 2, 2);
                H(g, accBlue, 6, B + 8, 2, 2); H(g, Brushes.White, 6, B + 10, 2, 1);
                break;
            case 6: // 毛线围巾
                H(g, C(80, 165, 95), 4, B + 6, 16, 2);
                for (int x = 5; x < 19; x += 3) H(g, C(60, 135, 75), x, B + 7, 1, 1);
                H(g, C(80, 165, 95), 10, B + 8, 2, 3);
                H(g, C(250, 215, 90), 10, B + 11, 2, 1);
                break;
            case 7: // 格子围巾
                H(g, C(200, 50, 50), 4, B + 6, 16, 2);
                H(g, C(240, 200, 90), 4, B + 7, 16, 1);
                for (int x = 5; x < 20; x += 4) H(g, C(120, 30, 35), x, B + 6, 1, 2);
                H(g, C(200, 50, 50), 15, B + 8, 2, 2); H(g, C(120, 30, 35), 15, B + 8, 1, 2);
                break;
            case 8: // 珍珠项链
                for (int x = 5; x <= 18; x += 2) H(g, C(252, 250, 245), x, (x >= 9 && x <= 14) ? B + 8 : B + 7, 1, 1);
                H(g, C(240, 160, 190), 11, B + 9, 2, 1);
                break;
            case 9: // 领巾
                {
                    Brush rb = C(215, 55, 60);
                    H(g, rb, 6, B + 6, 12, 1); H(g, rb, 7, B + 7, 10, 1); H(g, rb, 9, B + 8, 6, 1); H(g, rb, 11, B + 9, 2, 1);
                    H(g, Brushes.White, 8, B + 6, 1, 1); H(g, Brushes.White, 13, B + 7, 1, 1);
                    H(g, Brushes.White, 11, B + 8, 1, 1); H(g, Brushes.White, 16, B + 6, 1, 1);
                }
                break;
        }

        accDx = edx;
        int bx1 = 2 * e1 + 3, bw = (2 * e2 - 1) - bx1;
        switch (outfit[1])
        {
            case 1: // 墨镜
                foreach (int e in eyes)
                {
                    H(g, accBlack, 2 * e - 1, EY, 4, 3); H(g, accBlack, 2 * e, EY + 3, 2, 1);
                    H(g, Brushes.White, 2 * e, EY, 1, 1);
                }
                H(g, accBlack, bx1, EY, bw, 1);
                break;
            case 2: // 圆眼镜
                foreach (int e in eyes)
                {
                    H(g, accBrown, 2 * e, EY - 1, 2, 1); H(g, accBrown, 2 * e, EY + 4, 2, 1);
                    H(g, accBrown, 2 * e - 1, EY, 1, 4); H(g, accBrown, 2 * e + 2, EY, 1, 4);
                }
                H(g, accBrown, bx1, EY + 1, bw, 1);
                break;
            case 3: // 腮红
                H(g, accBlush, Math.Max(4, 2 * e1 - 2), EY + 3, 2, 1);
                H(g, accBlush, Math.Min(18, 2 * e2 + 2), EY + 3, 2, 1);
                break;
            case 4: // 小胡子
                int c = e1 + e2 + 1;
                H(g, accBrown, c - 3, EY + 4, 6, 1);
                H(g, accBrown, c - 4, EY + 3, 1, 1); H(g, accBrown, c + 3, EY + 3, 1, 1);
                break;
            case 5: // 爱心眼镜
                {
                    Brush hb = C(240, 80, 120);
                    foreach (int e in eyes)
                    {
                        PH(g, hb, 2 * e - 1, EY, "XX.XX", "XXXXX", ".XXX.", "..X..");
                        H(g, Brushes.White, 2 * e - 1, EY, 1, 1);
                    }
                    H(g, hb, 2 * e1 + 4, EY + 1, (2 * e2 - 1) - (2 * e1 + 4), 1);
                }
                break;
            case 6: // 星星眼镜
                {
                    Brush sb = C(250, 200, 50);
                    foreach (int e in eyes)
                    {
                        PH(g, sb, 2 * e - 1, EY - 1, "..X..", "XXXXX", ".XXX.", ".XXX.", ".X.X.");
                        H(g, dark, 2 * e, EY + 1, 2, 1);
                    }
                    H(g, sb, 2 * e1 + 4, EY + 1, (2 * e2 - 1) - (2 * e1 + 4), 1);
                }
                break;
            case 7: // 单片眼镜（戴在右眼）
                H(g, accGold, 2 * e2, EY - 1, 2, 1); H(g, accGold, 2 * e2, EY + 4, 2, 1);
                H(g, accGold, 2 * e2 - 1, EY, 1, 4); H(g, accGold, 2 * e2 + 2, EY, 1, 4);
                H(g, accGold, 2 * e2 + 2, EY + 5, 1, 1); H(g, accGold, 2 * e2 + 1, EY + 7, 1, 1); H(g, accGold, 2 * e2 + 2, EY + 9, 1, 1);
                break;
            case 8: // 方框眼镜
                foreach (int e in eyes)
                {
                    H(g, accBlack, 2 * e - 1, EY - 1, 4, 1); H(g, accBlack, 2 * e - 1, EY + 4, 4, 1);
                    H(g, accBlack, 2 * e - 1, EY, 1, 4); H(g, accBlack, 2 * e + 2, EY, 1, 4);
                }
                H(g, accBlack, bx1, EY, bw, 1);
                break;
            case 9: // 眼罩（左眼）
                H(g, accBlack, 2 * e1 - 1, EY, 4, 4);
                H(g, accBlack, 4, EY + 1, Math.Max(1, 2 * e1 - 5), 1);
                H(g, accBlack, 2 * e1 + 2, EY - 2, 1, 2); H(g, accBlack, 2 * e1 + 3, B, 1, 1);
                break;
            case 10: // 口罩
                H(g, C(200, 205, 215), 4, EY + 4, 3, 1); H(g, C(200, 205, 215), 17, EY + 4, 3, 1);
                H(g, C(225, 238, 250), 7, EY + 4, 10, 3);
                H(g, C(185, 210, 235), 8, EY + 5, 8, 1);
                break;
        }

        accDx = 0;
        switch (outfit[0])
        {
            case 1: // 礼帽
                H(g, accBlack, 6, HB - 1, 12, 1);
                H(g, accBlack, 8, HB - 6, 8, 5);
                H(g, accRed, 8, HB - 2, 8, 1);
                break;
            case 2: // 派对帽
                H(g, accPink, 8, HB - 1, 8, 1); H(g, accBlue, 9, HB - 2, 6, 1);
                H(g, accPink, 10, HB - 3, 4, 1); H(g, accBlue, 11, HB - 4, 2, 1);
                H(g, accGold, 11, HB - 6, 2, 2);
                break;
            case 3: // 皇冠
                H(g, accGold, 7, HB - 2, 10, 2);
                H(g, accGold, 7, HB - 3, 2, 1); H(g, accGold, 11, HB - 4, 2, 2); H(g, accGold, 15, HB - 3, 2, 1);
                H(g, accRed, 11, HB - 2, 2, 1);
                H(g, accBlue, 8, HB - 1, 1, 1); H(g, accBlue, 15, HB - 1, 1, 1);
                break;
            case 4: // 蝴蝶结
                H(g, accPink, 13, HB - 3, 2, 3); H(g, accPink, 17, HB - 3, 2, 3);
                H(g, accPinkDark, 15, HB - 2, 2, 2);
                break;
            case 5: // 毛线帽
                H(g, Brushes.White, 6, HB - 1, 12, 1);
                H(g, accBlue, 7, HB - 2, 10, 1); H(g, accBlue, 8, HB - 3, 8, 1); H(g, accBlue, 10, HB - 4, 4, 1);
                H(g, Brushes.White, 11, HB - 6, 2, 2);
                break;
            case 6: // 耳机
                H(g, accGray, 4, HB - 2, 16, 1);
                H(g, accGray, 3, HB - 2, 1, B - HB + 3); H(g, accGray, 20, HB - 2, 1, B - HB + 3);
                H(g, accGray, 2, B + 1, 2, 4); H(g, accGray, 20, B + 1, 2, 4);
                H(g, accPink, 3, B + 2, 1, 2); H(g, accPink, 20, B + 2, 1, 2);
                break;
            case 7: // 草帽
                H(g, C(230, 195, 110), 2, HB - 1, 20, 1);
                H(g, C(230, 195, 110), 7, HB - 4, 10, 3);
                H(g, C(200, 160, 80), 2, HB - 1, 1, 1); H(g, C(200, 160, 80), 21, HB - 1, 1, 1);
                H(g, accRed, 7, HB - 2, 10, 1);
                break;
            case 8: // 棒球帽
                H(g, C(215, 55, 60), 7, HB - 3, 10, 3);
                H(g, C(215, 55, 60), 9, HB - 4, 6, 1);
                H(g, C(165, 35, 45), 16, HB - 1, 6, 1);
                H(g, Brushes.White, 11, HB - 5, 2, 1);
                H(g, Brushes.White, 11, HB - 2, 2, 1);
                break;
            case 9: // 厨师帽
                H(g, Brushes.White, 5, HB - 6, 14, 3);
                H(g, Brushes.White, 6, HB - 7, 12, 1);
                H(g, Brushes.White, 7, HB - 3, 10, 3);
                H(g, C(205, 208, 218), 7, HB - 3, 10, 1);
                H(g, C(205, 208, 218), 9, HB - 6, 1, 2); H(g, C(205, 208, 218), 14, HB - 6, 1, 2);
                break;
            case 10: // 猫耳
                {
                    Brush d = C(190, 95, 70);   // 比身体深一点的橙色，深色桌面上也看得见
                    H(g, d, 4, HB - 1, 16, 1);
                    PH(g, d, 5, HB - 4, "X..", "XX.", "XXX");
                    PH(g, d, 16, HB - 4, "..X", ".XX", "XXX");
                    H(g, accPink, 6, HB - 2, 1, 1); H(g, accPink, 17, HB - 2, 1, 1);
                }
                break;
            case 11: // 花环
                H(g, C(80, 165, 95), 4, HB - 1, 16, 1);
                H(g, C(250, 140, 180), 5, HB - 2, 2, 2); H(g, C(255, 215, 80), 9, HB - 2, 2, 2);
                H(g, Brushes.White, 13, HB - 2, 2, 2); H(g, C(250, 140, 180), 17, HB - 2, 2, 2);
                H(g, C(80, 165, 95), 8, HB - 2, 1, 1); H(g, C(80, 165, 95), 15, HB - 2, 1, 1);
                break;
            case 12: // 魔法帽
                {
                    Brush p = C(110, 70, 170);
                    H(g, p, 3, HB - 1, 18, 1);
                    H(g, p, 7, HB - 3, 10, 2); H(g, p, 9, HB - 5, 6, 2); H(g, p, 11, HB - 7, 4, 2); H(g, p, 14, HB - 8, 3, 1);
                    H(g, accGold, 7, HB - 2, 10, 1);
                    H(g, accGold, 11, HB - 5, 1, 1);
                }
                break;
            case 13: // 光环
                {
                    Brush hl = C(255, 225, 90);
                    H(g, hl, 8, HB - 5, 8, 1); H(g, hl, 8, HB - 3, 8, 1);
                    H(g, hl, 7, HB - 4, 1, 1); H(g, hl, 16, HB - 4, 1, 1);
                }
                break;
        }
    }

    void DrawComputer(Graphics g)
    {
        int by = TOP + 1;                      // 坐下来，身体低一格
        int half = SZ / 2;
        Pal3 body = BodyPal();
        Pal3 armL = SleevePal(0) ?? body, armR = SleevePal(1) ?? body;
        accDx = 0;
        if (outfit[3] == 6) H(g, coatCape, 2, 2 * by + 3, 20, 9);
        Block(g, body, 2, 2 * (TOP + 6), 2, 2);
        Block(g, body, 20, 2 * (TOP + 6), 2, 2);
        Block(g, body, 4, 2 * by, 16, 10);
        DrawCoat(g, 2 * by);
        double ty = Math.Sin(TAU * ((tick / 2) % 5) / 5);   // 打字：5 帧，左右手交替
        Block(g, armL, 0, 2 * (by + 2), 4, 4, (int)Math.Round((1 + ty) / 2 * SZ));
        Block(g, armR, 20, 2 * (by + 2), 4, 4, (int)Math.Round((1 - ty) / 2 * SZ));
        int a = 26 + 16 * ((tick / 4 * 7) % 3);
        using (Brush glow = new SolidBrush(Color.FromArgb(a, 150, 205, 255)))
            R(g, glow, 2 * SZ, (by + 1) * SZ, 8 * SZ, 3 * SZ);
        int dx = (int)Math.Round(Math.Sin(TAU * ((tick / 35) % 5) / 5) * half);   // 眼睛在屏幕上慢慢扫
        int ey = (by + 1) * SZ + half;
        int[] eyes = { 3, 8 };
        double open = (blink >= 1 && blink <= 6) ? BlinkOpen[blink] : 1.0;
        foreach (int ex in eyes)
        {
            int eh = open <= 0 ? half : Math.Max(half, (int)Math.Round((SZ + half) * open));
            R(g, dark, ex * SZ + dx, ey + SZ + half - eh, SZ, eh);
        }
        DrawAcc(g, 2 * by, 2 * by, 2 * (by + 1) + 1, dx, eyes);
        int ly = (TOP + 4) * SZ + half;
        int lh = ROWS * SZ - ly - half;
        R(g, lidEdge, 3 * SZ, ly, 6 * SZ, lh);
        int bd = Math.Max(1, SZ / 4);
        R(g, lid, 3 * SZ + bd, ly + bd, 6 * SZ - 2 * bd, lh - bd);
        using (Brush logo = new SolidBrush((tick / 12) % 2 == 0 ? Color.FromArgb(255, 255, 255) : Color.FromArgb(170, 205, 255)))
            R(g, logo, 6 * SZ - half, ly + lh / 2 - half, 2 * half, 2 * half);
        R(g, baseGray, 2 * SZ, ROWS * SZ - half, 8 * SZ, half);
    }

    // 自检：平时的 7 种小动作 + 墙上的 6 种待机，每种 5 帧
    public static void RenderIdles(string path)
    {
        PetForm f = new PetForm(true);
        int W = f.ClientSize.Width, pad = 12, perRow = 5;
        int[][] at = {
            new[] { 10, 30, 60, 90, 115 }, new[] { 0, 4, 8, 12, 20 }, new[] { 10, 30, 50, 70, 80 },
            new[] { 0, 5, 9, 13, 17 }, new[] { 4, 8, 12, 16, 20 }, new[] { 2, 4, 6, 8, 10 }, new[] { 10, 45, 60, 74, 90 } };
        int rows = 7 + 6;
        using (Bitmap bmp = new Bitmap(pad + perRow * (W + pad), pad + rows * (W + pad)))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 44, 52));
                for (int row = 0; row < rows; row++)
                    for (int k = 0; k < perRow; k++)
                    {
                        f.parts.Clear(); f.drag = false; f.side = 0; f.vdir = 0; f.happy = 0; f.blink = 0; f.squash = 0;
                        f.shake = 0; f.blushT = 0; f.lookDir = 0; f.outfit = new int[SLOTS]; f.inNest = false; f.nestDraw = false;
                        f.idleAct = 0; f.clingAct = 0;
                        if (row < 7) { f.state = St.Idle; f.idleAct = row + 1; f.actT = at[row][k]; f.tick = at[row][k] * 3; }
                        else
                        {
                            f.state = St.Cling; f.side = row % 2 == 0 ? 1 : -1; f.clingAct = row - 6;
                            f.actT = new[] { 5, 30, 50, 70, 100 }[k]; f.tick = k * 7;
                        }
                        g.TranslateTransform(pad + k * (W + pad), pad + row * (W + pad));
                        f.Draw(g);
                        g.ResetTransform();
                    }
            }
            bmp.Save(path);
        }
    }

    // ---------- 自检：把各种画面画成图 ----------
    public static void Render(string path)
    {
        PetForm f = new PetForm(true);
        int W = f.ClientSize.Width, pad = 12, n = 45, perRow = 5, S = f.SZ;
        using (Bitmap bmp = new Bitmap(Math.Max(pad + perRow * (W + pad), Foods.Length * 30 + pad), pad + 10 * (W + pad)))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 44, 52));
                for (int i = 0; i < n; i++)
                {
                    f.parts.Clear();
                    f.drag = false; f.dragMoved = false; f.snap = 0; f.side = 0; f.vdir = 0;
                    f.frame = 0; f.tick = 0; f.blink = 0; f.happy = 0; f.squash = 0; f.dir = 1;
                    f.hot = 0; f.shake = 0; f.timer = 20; f.lookDir = 0;
                    f.giftPhase = 0; f.kickAnim = 0; f.chasing = false; f.inNest = false;
                    f.iceHP = 3; f.shiver = 0; f.bloat = 0; f.milkT = 0; f.sourT = 0; f.sleepyBye = false; f.bathBye = false; f.vy = 0; f.blushT = 0;
                    f.state = St.Idle; f.outfit = new int[SLOTS];
                    f.giftAnim = 0; f.giftDone = false; f.meal.Clear(); f.mealPos = 0; f.foodT = 1; f.bites = 0; f.eatTick = 0;
                    int k8 = i % 5;   // 每一行是同一个动作的 5 帧
                    switch (i / 5)
                    {
                        case 0: f.state = St.Walk; f.tick = (k8 * 16 + 4) / 5; break;                       // 走路
                        case 1: f.state = St.Dance; f.tick = (k8 * 24 + 4) / 5; break;                      // 跳舞
                        case 2: f.state = St.Bye; f.tick = 2 * k8; break;             // 挥手
                        case 3: if (k8 == 0) { f.happy = 3; f.blushT = 50; f.AddHearts(2); } if (k8 == 1 || k8 == 2) { f.state = St.Water; f.tick = 6 * k8; } if (k8 == 3) { f.state = St.Cling; f.side = -1; f.happy = 3; f.blushT = 50; } if (k8 == 4) { f.state = St.Water; f.tick = 3; f.outfit = new[] { 7, 0, 0, 4 }; } break;   // 摸头脸红 + 浇水
                        case 5:
                            if (k8 == 0) f.state = St.Frozen;
                            if (k8 == 1) { f.state = St.Frozen; f.iceHP = 1; }
                            if (k8 == 2) { f.sourT = 30; f.tick = 1; }
                            if (k8 == 3) f.bloat = 50;
                            if (k8 == 4) { f.milkT = 50; f.AddText("咕嘟咕嘟~", Color.FromArgb(110, 140, 200), f.tfont); }
                            break;
                        case 6:
                            if (k8 == 0) { f.state = St.Sleep; f.sleepyBye = true; f.tick = 10; f.AddText("唔…拜拜…", Color.FromArgb(120, 120, 170), f.tfont); }
                            if (k8 == 1) { f.bathBye = true; f.tick = 2; }
                            if (k8 == 2) { f.seedT = 0; for (int s = 0; s < 3; s++) { Particle p = new Particle(); p.Kind = 4; p.X = (2 + s * 3) * f.SZ; p.Y = (TOP + 2 + s % 2) * f.SZ; p.Life = 9; f.parts.Add(p); } }
                            if (k8 == 3) { f.state = St.Jump; f.vy = -6f * f.K; f.PopcornBurst(); foreach (Particle p in f.parts) p.Y -= 3 * f.SZ; }
                            if (k8 == 4) { f.state = St.Frozen; f.iceHP = 2; f.outfit = new[] { 5, 0, 3, 8 }; }
                            break;
                        case 4: f.squash = 5 - k8; if (k8 == 0) { f.state = St.Jump; f.vy = -9f * f.K; f.squash = 0; } break;   // 起跳 + 落地
                        case 7:   // 各种吃法：喝咖啡、吸牛奶、舔冰淇淋、含薄荷糖、抛爆米花
                            {
                                int[] fi = { LOBSTER, MILK, ICE, NOODLES, FRIES };
                                int[] et = { 40, 5, 3, 10, 4 }, bt = { 2, 2, 1, 1, 2 };
                                f.state = St.Eat; f.foodIdx = fi[k8]; f.eatTick = et[k8]; f.bites = bt[k8];
                                float tx, ty; f.FoodTarget(out tx, out ty); f.foodFromX = tx; f.foodFromY = ty;
                            }
                            break;
                        case 8:   // 套餐坐着吃 / 礼物：转花、拆情书、剥糖 / 贴墙摸头
                            if (k8 <= 1)
                            {
                                f.meal.AddRange(new[] { 2, 3, 12 }); f.state = St.Meal; f.mealPos = k8;
                                f.NextMealFood(); f.foodT = k8 == 0 ? 1 : 0.5f; f.eatTick = 6;
                            }
                            if (k8 == 2) { f.state = St.Gift; f.giftPhase = 1; f.giftIdx = 0; f.giftAnim = 30; }
                            if (k8 == 3) { f.state = St.Gift; f.giftPhase = 1; f.giftIdx = 3; f.giftDone = true; }
                            if (k8 == 4) { f.state = St.Gift; f.giftPhase = 1; f.giftIdx = 4; f.giftDone = true; f.giftAnim = 20; }
                            break;
                    }
                    g.TranslateTransform(pad + (i % perRow) * (W + pad), pad + (i / perRow) * (W + pad));
                    f.Draw(g);
                    g.ResetTransform();
                }
                // 最后两行：6 种玩具 + 16 种食物
                int ty0 = pad + 9 * (W + pad);
                for (int k = 0; k < 6; k++)
                {
                    g.TranslateTransform(pad + k * 60, ty0);
                    ToyForm.DrawToy(g, k, 6, 0.4f);
                    g.ResetTransform();
                }
                for (int k = 0; k < Foods.Length; k++) DrawArt(g, Foods[k], pad + k * 30, ty0 + 56, 3, 0);
            }
            bmp.Save(path, ImageFormat.Png);
        }

        // 小窝：5 种类型 × 2 行
        int NW = NestForm.NC * S, NH = NestForm.NR * S;
        using (Bitmap bmp = new Bitmap(pad + 5 * (NW + pad), pad + 2 * (NH + pad)))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 44, 52));
                for (int r = 0; r < 2; r++)
                    for (int t = 0; t < 5; t++)
                    {
                        NestLook L = new NestLook();
                        L.Type = t; L.Color = (t + r * 3) % NestForm.Colors.Length; L.Pattern = (t + r) % NestForm.PatternZh.Length;
                        L.Decor = r == 0 ? 2 : 511; L.FlagColor = t; L.PlushL = r == 0 ? 0 : 1 + t; L.PlushR = r == 0 ? 6 : (t + 2) % 6 + 1;
                        g.TranslateTransform(pad + t * (NW + pad), pad + r * (NH + pad));
                        NestForm.DrawNest(g, S, L, f, new[] { r == 1 ? 3 : 0, 0, 0, r == 1 ? 1 : 0 });
                        g.ResetTransform();
                    }
            }
            bmp.Save(path.Replace(".png", "_nests.png"), ImageFormat.Png);
        }

        // 窝里做事：睡觉 + 6 种
        using (Bitmap bmp = new Bitmap(pad + 4 * (NW + pad), pad + 2 * (NH + pad)))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 44, 52));
                for (int a = 0; a < 7; a++)
                {
                    NestLook L = new NestLook(); L.Type = a % 4; L.Decor = 2;
                    f.state = a == 0 ? St.Sleep : St.Nest; f.nestAct = a; f.tick = 3; f.nestBite = 1; f.nestSnack = 1; f.happy = 0; f.blink = 0;
                    g.TranslateTransform(pad + (a % 4) * (NW + pad), pad + (a / 4) * (NH + pad));
                    NestForm.DrawNest(g, S, L, f, new int[SLOTS]);
                    g.ResetTransform();
                }
            }
            bmp.Save(path.Replace(".png", "_nestacts.png"), ImageFormat.Png);
        }

        // 背景：3 种场景 × 4 种天气（小窝放在上面）
        int BW2 = BackdropForm.BC * S, BH2 = BackdropForm.BR * S;
        using (Bitmap bmp = new Bitmap(pad + 4 * (BW2 + pad), pad + 3 * (BH2 + pad)))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 44, 52));
                BackdropForm.FlashFrom = 0; BackdropForm.FlashTo = 2;
                for (int sc = 1; sc <= 3; sc++)
                    for (int w = 1; w <= 4; w++)
                    {
                        NestLook L = new NestLook(); L.Bg = sc; L.Wall = w - 1; L.RoomDecor = 255; L.OutDecor = 511; L.Decor = 2;
                        g.TranslateTransform(pad + (w - 1) * (BW2 + pad), pad + (sc - 1) * (BH2 + pad));
                        BackdropForm.Draw(g, S, L, w, w == 3 ? 6 : 40);
                        g.TranslateTransform((BW2 - NW) / 2, BH2 - NH);
                        f.state = St.Nest; f.nestAct = 4; f.tick = 3;
                        NestForm.DrawNest(g, S, L, f, new int[SLOTS]);
                        g.ResetTransform();
                    }
            }
            bmp.Save(path.Replace(".png", "_backdrops.png"), ImageFormat.Png);
        }
        // 灯光：夜晚小屋开着落地灯 / 星空 / 没有背景
        int LW = LightForm.LC * S, LH = LightForm.LR * S;
        using (Bitmap bmp = new Bitmap(pad + 3 * (LW + pad), pad + LH + pad))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(120, 130, 150));
                for (int k = 0; k < 3; k++)
                {
                    NestLook L = new NestLook(); L.Bg = k == 0 ? 1 : (k == 1 ? 3 : 0); L.RoomDecor = 255; L.OutDecor = 511;
                    int x0 = pad + k * (LW + pad), y0 = pad;
                    int bx = x0 + (LW - BackdropForm.BC * S) / 2, by = y0 + LH - BackdropForm.BR * S;
                    g.TranslateTransform(bx, by);
                    BackdropForm.Draw(g, S, L, 1, 40);
                    g.TranslateTransform((BackdropForm.BC * S - NW) / 2, BackdropForm.BR * S - NH);
                    f.state = St.Nest; f.nestAct = 5; f.movieGenre = 3; f.tick = 3;
                    NestForm.DrawNest(g, S, L, f, new int[SLOTS]);
                    g.ResetTransform();
                    // 旁边站着一只 Claude，看它被照到
                    f.state = St.Idle;
                    g.TranslateTransform(x0 + LW / 2 + 10 * S, y0 + LH - 12 * S);
                    f.Draw(g);
                    g.ResetTransform();
                    using (Bitmap lb = LightForm.Build(S, L, k == 2 ? 0.5f : 0.55f, k == 0, 1)) g.DrawImage(lb, x0, y0, LW, LH);
                }
            }
            bmp.Save(path.Replace(".png", "_light.png"), ImageFormat.Png);
        }

        // 浴缸：3 个阶段
        int BW = BathForm.BC * S, BH = BathForm.BR * S;
        using (Bitmap bmp = new Bitmap(pad + 3 * (BW + pad), pad + BH + pad))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(40, 44, 52));
                for (int k = 0; k < 3; k++)
                {
                    BathForm b = new BathForm(f, S, f.K);
                    b.ShowPetAlways = k > 0;
                    f.outfit = new int[SLOTS];
                    if (k == 1) { b.level = 0.8f; b.bubbly = 0.8f; b.spraying = true; b.foam = 9; b.wet = true; b.dragItem = 2; b.ix[2] = 18 * S; b.iy[2] = 7 * S; for (int z = 0; z < 30; z++) b.Step(); }
                    if (k == 2) { b.level = 1f; b.bubbly = 0.4f; b.scrubbed = true; b.clean = true; b.wet = true; b.dragItem = 3; b.ix[3] = 17 * S; b.iy[3] = 11 * S; b.foam = 0; }
                    g.TranslateTransform(pad + k * (BW + pad), pad);
                    b.DrawBath(g);
                    g.ResetTransform();
                }
            }
            bmp.Save(path.Replace(".png", "_bath.png"), ImageFormat.Png);
        }
    }

    // 自检：把设置窗口各个页面截图
    public static void RenderSettings(string path)
    {
        PetForm pet = new PetForm(true), pv = new PetForm(true);
        pet.outfit = new[] { 3, 2, 1, 3 };
        pet.Look.Type = 0; pet.Look.Decor = 1 | 2 | 8 | 32 | 64; pet.Look.PlushL = 1; pet.Look.PlushR = 6; pet.Look.Pattern = 1; pet.Look.FlagColor = 4; pet.Look.Light = 2; pet.Look.Bg = 3;
        int[] tabsToShoot = { 0, 0, 1, 1, 1, 1 };
        int[] scrolls = { 0, 9999, 0, 760, 9999, 9999 };
        for (int l = 0; l < tabsToShoot.Length; l++)
        {
            pet.En = l == 5;
            SettingsForm sf = new SettingsForm(pet, pv, pet.K);
            sf.StartPosition = FormStartPosition.Manual;
            sf.Location = new Point(-6000, -6000);
            sf.Show();
            sf.SelectTab(tabsToShoot[l]);
            Application.DoEvents();
            sf.ScrollTo(tabsToShoot[l], scrolls[l]);
            Application.DoEvents();
            using (Bitmap bmp = new Bitmap(sf.Width, sf.Height))
            {
                sf.DrawToBitmap(bmp, new Rectangle(0, 0, sf.Width, sf.Height));
                bmp.Save(path.Replace(".png", "_" + l + (pet.En ? "_en" : "_zh") + ".png"), ImageFormat.Png);
            }
            sf.Close();
        }
    }
}

// ---------- 小窝：可拖动、有重力、可装饰 ----------
class NestForm : Form
{
    internal const int NC = 24, NR = 11;
    readonly PetForm pet;
    readonly int SZ; readonly float K;
    internal float x, y, vy;
    internal bool drag;
    bool moved; float offX, offY; Point down;

    internal static readonly string[] TypeZh = { "篮子窝", "甜甜圈垫", "小帐篷", "纸箱", "小房子" };
    internal static readonly string[] TypeEn = { "Basket", "Donut", "Tent", "Box", "House" };
    internal static readonly Color[] Colors = {
        Color.FromArgb(110, 160, 230), Color.FromArgb(240, 150, 175), Color.FromArgb(120, 190, 125),
        Color.FromArgb(170, 135, 220), Color.FromArgb(175, 125, 85), Color.FromArgb(150, 152, 165),
        Color.FromArgb(240, 200, 90), Color.FromArgb(220, 90, 85) };
    internal static readonly string[] ColorZh = { "蓝色", "粉色", "绿色", "紫色", "棕色", "灰色", "黄色", "红色" };
    internal static readonly string[] ColorEn = { "Blue", "Pink", "Green", "Purple", "Brown", "Gray", "Yellow", "Red" };
    internal static readonly string[] PatternZh = { "纯色", "条纹", "波点", "格子", "星星" };
    internal static readonly string[] PatternEn = { "Plain", "Stripes", "Dots", "Plaid", "Stars" };
    internal static readonly string[] DecorZh = { "小花", "爱心", "星星串", "小旗子", "蝴蝶结", "气球", "彩灯", "盆栽", "月亮" };
    internal static readonly string[] DecorEn = { "Flower", "Heart", "Stars", "Flag", "Bow", "Balloon", "Lights", "Plant", "Moon" };
    internal static readonly Color[] FlagColors = {
        Color.FromArgb(230, 70, 70), Color.FromArgb(245, 150, 50), Color.FromArgb(250, 210, 60), Color.FromArgb(90, 180, 90),
        Color.FromArgb(80, 140, 230), Color.FromArgb(160, 110, 220), Color.FromArgb(245, 130, 175), Color.FromArgb(245, 245, 245) };
    internal static readonly string[] FlagZh = { "红", "橙", "黄", "绿", "蓝", "紫", "粉", "白" };
    internal static readonly string[] FlagEn = { "Red", "Orange", "Yellow", "Green", "Blue", "Purple", "Pink", "White" };
    internal static readonly string[] PlushZh = { "无", "小熊", "小兔", "小鸭", "小猫", "小恐龙", "迷你Claude" };
    internal static readonly string[] PlushEn = { "None", "Bear", "Bunny", "Duck", "Cat", "Dino", "Mini Claude" };
    internal static readonly string[][] Plush = {
        null,
        new[]{ "uu....uu", "uUuuuuUu", ".uuuuuu.", ".ukuuku.", ".uummuu.", "uuummuuu", "uuuuuuuu", ".uu..uu." },
        new[]{ ".ww..ww.", ".wp..pw.", ".wp..pw.", ".wwwwww.", ".wkwwkw.", ".wwPPww.", "wwwwwwww", ".ww..ww." },
        new[]{ "..hhhh..", ".hhhhhh.", ".hkhhkh.", ".hhoohh.", "hhhhhhhh", "hhhhhhhh", ".hhhhhh.", "..o..o.." },
        new[]{ "v......v", "vv....vv", "vpvvvvpv", "vvvvvvvv", "vkvvvvkv", "vvvPPvvv", ".vvvvvv.", ".vv..vv." },
        new[]{ "....gg..", "...gggg.", "...gkgg.", "G..gggg.", "Ggggggg.", ".gmmmgg.", ".gmmmg..", ".gg.gg.." },
        new[]{ "........", "........", ".OOOOOO.", ".OkOOkO.", "OOOOOOOO", ".OOOOOO.", ".O.OO.O.", ".O.OO.O." },
    };

    // 每种窝的位置参数：爱心、小花、蝴蝶结、星星串(x0,x1,y)、玩偶 y、是否有小手搭边、Claude 被遮挡的高度
    static readonly float[,] HeartAt = { { 12, 9.6f }, { 12, 9.8f }, { 5.6f, 7.6f }, { 8, 9.7f }, { 12, 2.9f } };
    static readonly float[,] FlowerAt = { { 4.2f, 8.6f }, { 3.6f, 9.6f }, { 2.4f, 10.2f }, { 4.4f, 8.7f }, { 5.4f, 9.6f } };
    static readonly float[,] BowAt = { { 19.8f, 8.6f }, { 20.4f, 9.6f }, { 12, 1.5f }, { 19.6f, 8.7f }, { 18.6f, 9.6f } };
    static readonly float[,] GarAt = { { 4.5f, 19.5f, 4.6f }, { 4.5f, 19.5f, 4.6f }, { 8.6f, 15.4f, 4.6f }, { 4.5f, 19.5f, 5.6f }, { 3.6f, 20.4f, 5.4f } };
    static readonly float[] PlushYAt = { 5.0f, 5.4f, 5.9f, 4.9f, 7.0f };
    static readonly bool[] PawsAt = { true, true, false, true, false };
    static readonly float[] ClipAt = { 9.5f, 10.4f, 10f, 9.5f, 0 };

    public NestForm(PetForm pet, int sz, float k)
    {
        this.pet = pet; SZ = sz; K = k;
        Text = "Claude Bed";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(255, 0, 255);
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        ClientSize = new Size(NC * sz, NR * sz);
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000; return cp; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    Rectangle WA() { return Screen.FromPoint(new Point((int)(x + Width / 2), (int)(y + Height / 2))).WorkingArea; }
    internal float Ground() { return WA().Bottom - Height; }

    internal void Step()
    {
        if (drag) return;
        Rectangle wa = WA();
        float ground = wa.Bottom - Height;
        if (y < ground) { vy += 1f * K; y += vy; if (y >= ground) { y = ground; vy = 0; } }
        else { y = ground; vy = 0; }
        if (x < wa.Left) x = wa.Left;
        if (x > wa.Right - Width) x = wa.Right - Width;
        Point p = new Point((int)x, (int)y);
        if (Location != p) Location = p;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Point c = Cursor.Position;
        drag = true; moved = false; offX = c.X - x; offY = c.Y - y; down = c;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!drag) return;
        Point c = Cursor.Position;
        if (!moved && Math.Abs(c.X - down.X) + Math.Abs(c.Y - down.Y) > 4) moved = true;
        if (!moved) return;
        x = c.X - offX; y = c.Y - offY; vy = 0;
        Location = new Point((int)x, (int)y);
        pet.OnNestMoved();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !drag) return;
        drag = false;
        if (!moved) pet.OnNestClicked(); else pet.OnNestDropped();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; }   // 点小窝不抢到最前面
        base.WndProc(ref m);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        DrawNest(e.Graphics, SZ, pet.Look, pet.InNest ? pet : null, pet.Outfit);
    }

    internal static Color Mix(Color a, Color b, float t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    static int P(float v, int sz) { return (int)Math.Round(v * sz); }
    static Rectangle RC(int sz, float x, float y, float w, float h)
    {
        int x0 = P(x, sz), y0 = P(y, sz);
        return new Rectangle(x0, y0, P(x + w, sz) - x0, P(y + h, sz) - y0);
    }
    static void FR(Graphics g, Brush b, int sz, float x, float y, float w, float h) { g.FillRectangle(b, RC(sz, x, y, w, h)); }
    static void FE(Graphics g, Brush b, int sz, float x, float y, float w, float h) { g.FillEllipse(b, RC(sz, x, y, w, h)); }
    static GraphicsPath PathR(int sz, params float[] r)
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        for (int i = 0; i + 3 < r.Length; i += 4) p.AddRectangle(RC(sz, r[i], r[i + 1], r[i + 2], r[i + 3]));
        return p;
    }
    static GraphicsPath PathPoly(int sz, params float[] xy)
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        Point[] pts = new Point[xy.Length / 2];
        for (int i = 0; i < pts.Length; i++) pts[i] = new Point(P(xy[2 * i], sz), P(xy[2 * i + 1], sz));
        p.AddPolygon(pts);
        return p;
    }

    // 皮肤：底色 + 花纹
    internal static void FillSkin(Graphics g, int sz, Color c, int pat, GraphicsPath path)
    {
        using (Brush b = new SolidBrush(c)) g.FillPath(b, path);
        if (pat == 0) return;
        GraphicsState st = g.Save();
        g.SetClip(path, CombineMode.Intersect);
        RectangleF bb = path.GetBounds();
        int x1 = (int)bb.Right + sz, y1 = (int)bb.Bottom + sz, x0 = Math.Max(0, (int)bb.Left - sz), y0 = Math.Max(0, (int)bb.Top - sz);
        int hs = Math.Max(1, sz / 2), q = Math.Max(1, sz / 4);
        using (Brush lb = new SolidBrush(Mix(c, Color.White, 0.45f)))
        using (Brush db = new SolidBrush(Mix(c, Color.Black, 0.2f)))
        {
            switch (pat)
            {
                case 1:
                    for (int x = 0; x < x1; x += 2 * sz) g.FillRectangle(lb, x, y0, hs, y1 - y0);
                    break;
                case 2:
                    for (int y = 0; y < y1; y += sz)
                        for (int x = ((y / sz) % 2) * sz; x < x1; x += 2 * sz) g.FillRectangle(lb, x + q, y + q, hs, hs);
                    break;
                case 3:
                    for (int x = 0; x < x1; x += 2 * sz) g.FillRectangle(db, x, y0, q, y1 - y0);
                    for (int y = 0; y < y1; y += 2 * sz) g.FillRectangle(db, x0, y, x1 - x0, q);
                    for (int x = sz; x < x1; x += 2 * sz) g.FillRectangle(lb, x, y0, q, y1 - y0);
                    break;
                case 4:
                    for (int y = 0; y < y1; y += 2 * sz)
                        for (int x = ((y / (2 * sz)) % 2) * (3 * sz / 2); x < x1; x += 3 * sz)
                        {
                            g.FillRectangle(lb, x + q, y, q, 3 * q);
                            g.FillRectangle(lb, x, y + q, 3 * q, q);
                        }
                    break;
            }
        }
        g.Restore(st);
    }
    static void Skin(Graphics g, int sz, Color c, int pat, GraphicsPath path) { using (path) FillSkin(g, sz, c, pat, path); }

    // 24x11 格的宠物窝；sleeper 不为空时画一只睡在里面的 Claude
    internal static void DrawNest(Graphics g, int sz, NestLook L, PetForm sleeper, int[] fit)
    {
        int t = L.Type, D = L.Decor, hs = Math.Max(1, sz / 2), u = Math.Max(1, sz / 4);
        Color c = Colors[L.Color];
        Color backC = Mix(c, Color.Black, 0.32f);
        int pat = L.Pattern;
        using (Brush back = new SolidBrush(backC))
        using (Brush light = new SolidBrush(Mix(c, Color.White, 0.42f)))
        using (Brush shade = new SolidBrush(Mix(c, Color.Black, 0.15f)))
        using (Brush cushion = new SolidBrush(Color.FromArgb(250, 240, 222)))
        using (Brush cushShade = new SolidBrush(Color.FromArgb(232, 214, 186)))
        using (Brush yellow = new SolidBrush(Color.FromArgb(255, 205, 60)))
        using (Brush grayB = new SolidBrush(Color.FromArgb(150, 150, 160)))
        using (Brush darkIn = new SolidBrush(Color.FromArgb(70, 55, 50)))
        {
            // ---- 最后面的装饰：气球、月亮、旗子 ----
            if ((D & 32) != 0)
            {
                FR(g, grayB, sz, 2.3f, 3.2f, 0.15f, 5.2f);
                using (Brush bl = new SolidBrush(Color.FromArgb(240, 95, 115))) FE(g, bl, sz, 1.0f, 0.3f, 2.6f, 3.0f);
                FE(g, Brushes.White, sz, 1.6f, 0.9f, 0.6f, 0.6f);
                using (Brush bl = new SolidBrush(Color.FromArgb(200, 70, 90))) FR(g, bl, sz, 2.1f, 3.2f, 0.5f, 0.3f);
            }
            if ((D & 256) != 0)
            {
                FR(g, grayB, sz, 17.6f, 0, 0.15f, 1.0f);
                GraphicsState ms = g.Save();
                using (GraphicsPath a = new GraphicsPath(), b2 = new GraphicsPath())
                {
                    a.AddEllipse(RC(sz, 16.4f, 0.8f, 2.6f, 2.6f));
                    b2.AddEllipse(RC(sz, 17.3f, 0.4f, 2.6f, 2.6f));
                    g.SetClip(a, CombineMode.Intersect);
                    g.SetClip(b2, CombineMode.Exclude);
                    FE(g, yellow, sz, 16.4f, 0.8f, 2.6f, 2.6f);
                }
                g.Restore(ms);
            }
            if ((D & 8) != 0)
            {
                using (Brush pole = new SolidBrush(Color.FromArgb(120, 90, 70)))
                using (Brush flag = new SolidBrush(FlagColors[L.FlagColor]))
                using (Brush flagEdge = new SolidBrush(Mix(FlagColors[L.FlagColor], Color.Black, 0.2f)))
                {
                    FR(g, pole, sz, 22.6f, 0.4f, 0.5f, 10.6f);
                    FR(g, yellow, sz, 22.5f, 0.1f, 0.7f, 0.4f);
                    FR(g, flagEdge, sz, 19.5f, 0.6f, 3.1f, 1.2f);
                    FR(g, flag, sz, 19.7f, 0.7f, 2.9f, 1.0f);
                    FR(g, flag, sz, 20.6f, 1.7f, 2.0f, 0.6f);
                }
            }

            // ---- 后面 ----
            switch (t)
            {
                case 0: // 篮子窝
                    FR(g, back, sz, 4, 4, 16, 1);
                    FR(g, back, sz, 3, 5, 18, 3.5f);
                    FR(g, cushion, sz, 4, 7, 16, 2);
                    FR(g, cushShade, sz, 4, 7, 16, 0.25f);
                    break;
                case 1: // 甜甜圈垫
                    Skin(g, sz, backC, pat, PathR(sz, 3.5f, 6, 17, 0.6f, 2.5f, 6.5f, 19, 1.6f));
                    FR(g, cushion, sz, 5, 7.2f, 14, 1.5f);
                    FR(g, cushShade, sz, 5, 7.2f, 14, 0.25f);
                    break;
                case 2: // 小帐篷
                    Skin(g, sz, c, pat, PathPoly(sz, 12, 0.6f, 1.6f, 9.8f, 22.4f, 9.8f));
                    using (GraphicsPath op = PathPoly(sz, 12, 2.4f, 5.4f, 9.8f, 18.6f, 9.8f)) g.FillPath(back, op);
                    using (GraphicsPath fl = PathPoly(sz, 12, 2.4f, 5.4f, 9.8f, 7.0f, 9.8f)) g.FillPath(light, fl);
                    using (GraphicsPath fr = PathPoly(sz, 12, 2.4f, 17.0f, 9.8f, 18.6f, 9.8f)) g.FillPath(light, fr);
                    FR(g, yellow, sz, 11.7f, 0.1f, 0.6f, 0.6f);
                    break;
                case 3: // 纸箱
                    using (GraphicsPath lf = PathPoly(sz, 4, 5, 3, 3.0f, 11, 3.6f, 12, 5)) g.FillPath(light, lf);
                    using (GraphicsPath rf = PathPoly(sz, 12, 5, 13, 3.6f, 21, 3.0f, 20, 5)) g.FillPath(light, rf);
                    FR(g, back, sz, 4, 5, 16, 3);
                    FR(g, darkIn, sz, 4, 7.4f, 16, 0.6f);
                    break;
                case 4: // 小房子
                    Skin(g, sz, Mix(c, Color.Black, 0.35f), pat, PathPoly(sz, 12, 0.2f, 1.2f, 4.9f, 22.8f, 4.9f));
                    Skin(g, sz, c, pat, PathR(sz, 3, 4.8f, 18, 6.2f));
                    using (GraphicsPath door = DoorPath(sz)) g.FillPath(darkIn, door);
                    FR(g, cushion, sz, 8.2f, 9.8f, 7.6f, 1.2f);
                    break;
            }

            bool garFirst = t == 0 || t == 1 || t == 3;
            if (garFirst) DrawGarlands(g, sz, t, D, yellow, light);

            // ---- 睡在里面的 Claude ----
            if (sleeper != null)
            {
                GraphicsState st = g.Save();
                if (t == 4) { using (GraphicsPath door = DoorPath(sz)) g.SetClip(door, CombineMode.Intersect); }
                else g.IntersectClip(new Rectangle(0, 0, NC * sz, P(ClipAt[t], sz)));
                g.TranslateTransform(6 * sz, sleeper.NestLift(sz));
                sleeper.DrawNester(g, sz, fit);
                g.Restore(st);
            }

            // ---- 玩偶 ----
            float py = PlushYAt[t];
            if (L.PlushL > 0) PetForm.DrawArt(g, Plush[L.PlushL], P(3.6f, sz), P(py, sz), hs, 0);
            if (L.PlushR > 0) PetForm.DrawArt(g, Plush[L.PlushR], P(16.4f, sz), P(py, sz), hs, 0);

            // ---- 前面 ----
            switch (t)
            {
                case 0:
                    Skin(g, sz, c, pat, PathR(sz, 2, 8, 20, 2, 3, 10, 18, 1));
                    FR(g, light, sz, 3, 8, 18, 0.5f);
                    FR(g, shade, sz, 4, 10.75f, 16, 0.25f);
                    break;
                case 1:
                    Skin(g, sz, c, pat, PathR(sz, 2.5f, 8, 19, 1, 1.5f, 9, 21, 1.5f, 2.5f, 10.5f, 19, 0.5f));
                    FR(g, light, sz, 3, 8, 18, 0.4f);
                    break;
                case 2:
                    FR(g, cushion, sz, 1, 9.6f, 22, 1.4f);
                    FR(g, cushShade, sz, 1, 10.7f, 22, 0.3f);
                    break;
                case 3:
                    Skin(g, sz, c, pat, PathR(sz, 3, 8, 18, 3));
                    FR(g, light, sz, 3, 8.4f, 18, 0.5f);
                    FR(g, Brushes.White, sz, 15.6f, 9.3f, 3.4f, 1.0f);
                    FR(g, grayB, sz, 16.0f, 9.6f, 2.6f, 0.15f);
                    FR(g, grayB, sz, 16.0f, 9.9f, 1.8f, 0.15f);
                    break;
                case 4:
                    FR(g, light, sz, 7.6f, 10.6f, 8.8f, 0.4f);
                    break;
            }
            if (sleeper != null && PawsAt[t] && sleeper.NestLift(sz) == 0)
            {
                Color sl = PetForm.SleeveColor(fit[3], 0), sr = PetForm.SleeveColor(fit[3], 1);
                using (Brush pl = new SolidBrush(sl.IsEmpty ? PetForm.BodyCol : sl))
                using (Brush pr = new SolidBrush(sr.IsEmpty ? PetForm.BodyCol : sr))
                {
                    FR(g, pl, sz, 6.5f, 8, 1, 0.5f);
                    FR(g, pr, sz, 16.5f, 8, 1, 0.5f);
                }
            }
            if (!garFirst) DrawGarlands(g, sz, t, D, yellow, light);

            // ---- 前面的装饰 ----
            if ((D & 2) != 0)
            {
                using (Brush hb = new SolidBrush(L.Color == 1 || L.Color == 7 ? Color.White : Color.FromArgb(240, 80, 120)))
                {
                    int p = hs, x0 = P(HeartAt[t, 0], sz) - (5 * p) / 2, y0 = P(HeartAt[t, 1], sz) - 2 * p;
                    g.FillRectangle(hb, x0 + p, y0, p, p);
                    g.FillRectangle(hb, x0 + 3 * p, y0, p, p);
                    g.FillRectangle(hb, x0, y0 + p, 5 * p, p);
                    g.FillRectangle(hb, x0 + p, y0 + 2 * p, 3 * p, p);
                    g.FillRectangle(hb, x0 + 2 * p, y0 + 3 * p, p, p);
                }
            }
            int q = Math.Max(1, (int)Math.Round(sz * 0.55));
            if ((D & 1) != 0)
            {
                int cx = P(FlowerAt[t, 0], sz), cy = P(FlowerAt[t, 1], sz);
                Brush petal = Brushes.White;
                g.FillRectangle(petal, cx - q / 2, cy - 3 * q / 2, q, q);
                g.FillRectangle(petal, cx - q / 2, cy + q / 2, q, q);
                g.FillRectangle(petal, cx - 3 * q / 2, cy - q / 2, q, q);
                g.FillRectangle(petal, cx + q / 2, cy - q / 2, q, q);
                g.FillRectangle(yellow, cx - q / 2, cy - q / 2, q, q);
            }
            if ((D & 16) != 0)
            {
                int cx = P(BowAt[t, 0], sz), cy = P(BowAt[t, 1], sz);
                bool pinkish = L.Color == 1;
                using (Brush loop = new SolidBrush(pinkish ? Color.FromArgb(215, 45, 55) : Color.FromArgb(250, 120, 165)))
                using (Brush knot = new SolidBrush(pinkish ? Color.FromArgb(160, 30, 40) : Color.FromArgb(215, 60, 120)))
                {
                    g.FillRectangle(loop, cx - 2 * q, cy - q, q + q / 2, 2 * q);
                    g.FillRectangle(loop, cx + q / 2, cy - q, q + q / 2, 2 * q);
                    g.FillRectangle(knot, cx - q / 2, cy - q / 2, q, q);
                }
            }
            if ((D & 128) != 0)   // 盆栽
            {
                using (Brush leaf = new SolidBrush(Color.FromArgb(80, 170, 90)))
                using (Brush leaf2 = new SolidBrush(Color.FromArgb(55, 135, 70)))
                using (Brush pot = new SolidBrush(Color.FromArgb(200, 110, 70)))
                using (Brush potRim = new SolidBrush(Color.FromArgb(170, 85, 55)))
                {
                    FR(g, leaf, sz, 0.6f, 6.6f, 0.6f, 2.4f);
                    FR(g, leaf2, sz, 1.3f, 6.0f, 0.6f, 3.0f);
                    FR(g, leaf, sz, 1.9f, 6.8f, 0.6f, 2.2f);
                    FR(g, leaf2, sz, 0.1f, 7.4f, 0.6f, 1.4f);
                    FR(g, potRim, sz, 0.1f, 8.8f, 2.6f, 0.5f);
                    FR(g, pot, sz, 0.3f, 9.3f, 2.2f, 1.7f);
                }
            }
        }
    }

    static GraphicsPath DoorPath(int sz)
    {
        GraphicsPath p = new GraphicsPath(FillMode.Winding);
        p.AddRectangle(RC(sz, 8, 6.2f, 8, 4.8f));
        p.AddEllipse(RC(sz, 8, 4.4f, 8, 3.6f));
        return p;
    }

    static void DrawGarlands(Graphics g, int sz, int t, int D, Brush yellow, Brush light)
    {
        float x0 = GarAt[t, 0], x1 = GarAt[t, 1], gy = GarAt[t, 2];
        int u = Math.Max(1, sz / 4);
        bool stars = (D & 4) != 0, lights = (D & 64) != 0;
        if ((stars || lights) && t == 1)
            using (Brush post = new SolidBrush(Color.FromArgb(150, 120, 95)))
            {
                FR(g, post, sz, x0 - 0.2f, gy, 0.3f, 7 - gy);
                FR(g, post, sz, x1 - 0.1f, gy, 0.3f, 7 - gy);
            }
        if (stars)
        {
            g.FillRectangle(light, P(x0, sz), P(gy, sz), P(x1, sz) - P(x0, sz), Math.Max(1, u / 2));
            for (float sx = x0 + 1f; sx < x1 - 0.5f; sx += 3f)
            {
                int cx = P(sx, sz), cy = P(gy, sz);
                g.FillRectangle(yellow, cx - u / 2, cy - 3 * u / 2, u, 3 * u);
                g.FillRectangle(yellow, cx - 3 * u / 2, cy - u / 2, 3 * u, u);
            }
        }
        if (lights)
        {
            float ly = stars ? gy + 1.1f : gy;
            Color[] bulbs = { Color.FromArgb(240, 80, 80), Color.FromArgb(255, 210, 60), Color.FromArgb(90, 200, 110), Color.FromArgb(90, 160, 255) };
            using (Brush wire = new SolidBrush(Color.FromArgb(70, 70, 75)))
                g.FillRectangle(wire, P(x0, sz), P(ly, sz), P(x1, sz) - P(x0, sz), Math.Max(1, u / 2));
            int k = 0;
            for (float sx = x0 + 0.6f; sx < x1 - 0.3f; sx += 1.6f, k++)
                using (Brush bb = new SolidBrush(bulbs[k % 4]))
                    g.FillRectangle(bb, P(sx, sz), P(ly, sz) + Math.Max(1, u / 2), Math.Max(2, sz / 2), Math.Max(2, (int)(sz * 0.6)));
        }
    }
}

// ---------- 浴缸：花洒、肥皂、沐浴露、浴巾 ----------
class BathForm : Form
{
    internal const int BC = 34, BR = 20;
    internal const int PetCol = 13, PetRow = 7;   // Claude 坐在浴缸里的位置
    readonly PetForm pet;
    readonly int SZ; readonly float K;
    internal float x, y;
    int tick;
    internal float level, bubbly;
    float rub, dry;
    internal int foam;
    internal bool scrubbed, clean, wet, spraying;
    bool dried, saidSpray, saidTowel, saidFoam;
    int finishT;
    internal int dragItem;   // 0 无, 1 肥皂, 2 沐浴露, 3 浴巾
    internal bool ShowPetAlways;   // 只给自检截图用
    float dox, doy; Point lastM;
    internal readonly float[] ix = new float[4], iy = new float[4];
    readonly float[] iw = new float[4], ih = new float[4], hx = new float[4], hy = new float[4];
    readonly List<BP> ps = new List<BP>();
    readonly Random rand = new Random();
    readonly Font hintFont = new Font("Microsoft YaHei UI", 7.5f);
    class BP { public float X, Y, VX, VY; public int Kind, Life; }
    // 泡沫在 Claude 身上的位置（格）：x, y, 半径
    static readonly float[,] FoamPos = {
        { 15.6f, 11.7f, 0.8f }, { 17.6f, 11.4f, 0.9f }, { 19.8f, 11.5f, 0.8f }, { 21.8f, 11.7f, 0.8f },
        { 14.6f, 13.4f, 0.7f }, { 23.2f, 13.6f, 0.7f }, { 14.9f, 15.0f, 0.7f }, { 22.9f, 14.9f, 0.7f },
        { 16.4f, 15.5f, 0.7f }, { 19.0f, 15.7f, 0.8f }, { 21.4f, 15.4f, 0.7f }, { 18.6f, 10.8f, 0.7f } };

    public BathForm(PetForm pet, int sz, float k)
    {
        this.pet = pet; SZ = sz; K = k;
        Text = "Claude Bath";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(255, 0, 255);
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        ClientSize = new Size(BC * sz, BR * sz);
        SetHome(1, 0.5f, 14.4f, 2.2f, 1.0f);    // 肥皂
        SetHome(2, 3.0f, 11.6f, 1.6f, 4.3f);    // 沐浴露
        SetHome(3, 29.2f, 6.2f, 2.6f, 6.0f);    // 浴巾
    }
    void SetHome(int i, float cx, float cy, float w, float h)
    {
        hx[i] = cx * SZ; hy[i] = cy * SZ; iw[i] = w * SZ; ih[i] = h * SZ; ix[i] = hx[i]; iy[i] = hy[i];
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000; return cp; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    RectangleF ItemRect(int i) { return new RectangleF(ix[i], iy[i], iw[i], ih[i]); }
    RectangleF PetRect() { return new RectangleF(14.2f * SZ, 11f * SZ, 9.6f * SZ, 6f * SZ); }
    RectangleF ShowerRect() { return new RectangleF(16f * SZ, 1f * SZ, 7f * SZ, 3.6f * SZ); }
    float WaterTop() { return (17.5f - level * 2.5f) * SZ; }
    bool Pouring()
    {
        if (dragItem != 2 || level < 0.12f) return false;
        float mx = ix[2] + iw[2] / 2, my = iy[2] + ih[2];
        return mx > 10 * SZ && mx < 28 * SZ && my < WaterTop();
    }

    void Add(int kind, float px, float py, float vx, float vy, int life)
    {
        BP p = new BP(); p.Kind = kind; p.X = px; p.Y = py; p.VX = vx; p.VY = vy; p.Life = life; ps.Add(p);
    }
    float Rnd() { return (float)rand.NextDouble(); }

    // 洗澡时说拜拜：冒一串泡泡
    internal void ByeBubbles()
    {
        spraying = false;
        for (int i = 0; i < 12; i++) Add(3, (12 + Rnd() * 14) * SZ, (13 + Rnd() * 3) * SZ, (Rnd() - 0.5f) * 0.6f * K, -(0.5f + Rnd() * 0.8f) * K, 60);
        Sparkles(6);
    }

    void Sparkles(int n)
    {
        for (int i = 0; i < n; i++) Add(2, (14 + Rnd() * 10) * SZ, (9 + Rnd() * 7) * SZ, (Rnd() - 0.5f) * 1.5f * K, -(0.4f + Rnd()) * K, 18 + rand.Next(10));
    }

    void Clean()
    {
        clean = true;
        pet.BathSay("洗干净啦!", "All clean!");
        Sparkles(10);
    }

    void Finish()
    {
        finishT = 50; spraying = false;
        pet.BathSay("香喷喷~", "So fresh~");
        pet.BathHearts(4);
        Sparkles(16);
    }

    internal void Step()
    {
        tick++;
        float wt = WaterTop();
        if (spraying)
        {
            level = Math.Min(1f, level + 0.0035f);
            wet = true; dried = false;
            for (int k = 0; k < 2; k++) Add(0, (17.4f + Rnd() * 3.2f) * SZ, 3.6f * SZ, (Rnd() - 0.5f) * 0.6f * K, (3f + Rnd() * 2f) * K, 60);
            if (foam > 0 && tick % 10 == 0) { foam--; if (foam == 0 && scrubbed && !clean) Clean(); }
        }
        if (Pouring() && tick % 3 == 0) Add(1, ix[2] + iw[2] / 2, iy[2] + ih[2], 0, 1.5f * K, 80);
        if (bubbly > 0.2f && Rnd() < bubbly * 0.12f && level > 0.1f)
            Add(3, (11f + Rnd() * 16f) * SZ, wt, (Rnd() - 0.5f) * 0.4f * K, -(0.4f + Rnd() * 0.5f) * K, 50);

        for (int i = ps.Count - 1; i >= 0; i--)
        {
            BP p = ps[i];
            p.Life--;
            if (p.Kind == 0 || p.Kind == 1)
            {
                p.VY += 0.25f * K; p.X += p.VX; p.Y += p.VY;
                bool inTub = p.X > 10 * SZ && p.X < 28 * SZ;
                bool onPet = p.Kind == 0 && p.X > 14.5f * SZ && p.X < 23.5f * SZ && p.Y > 11.5f * SZ;
                if ((inTub && p.Y >= wt) || onPet)
                {
                    if (p.Kind == 1) bubbly = Math.Min(1f, bubbly + 0.05f);
                    else if (rand.Next(3) == 0) Add(4, p.X, p.Y, (Rnd() - 0.5f) * 2f * K, -(1f + Rnd()) * K, 8);
                    p.Life = 0;
                }
            }
            else if (p.Kind == 4) { p.VY += 0.3f * K; p.X += p.VX; p.Y += p.VY; }
            else { p.X += p.VX; p.Y += p.VY; }
            if (p.Life <= 0 || p.Y > BR * SZ || p.Y < 0) ps.RemoveAt(i);
        }

        if (finishT > 0 && --finishT == 0) { pet.EndBath(true); return; }
        Invalidate();
    }

    // ---------- 鼠标 ----------
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || finishT > 0 || pet.Quitting) return;
        Point p = e.Location;
        for (int i = 3; i >= 1; i--)
            if (ItemRect(i).Contains(p))
            {
                dragItem = i; dox = p.X - ix[i]; doy = p.Y - iy[i]; lastM = p;
                Invalidate(); return;
            }
        if (ShowerRect().Contains(p))
        {
            spraying = !spraying;
            if (spraying && !saidSpray) { saidSpray = true; pet.BathSay("哗啦啦~", "Splash~"); }
        }
        else if (PetRect().Contains(p)) pet.BathHearts(1);   // 摸摸头
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragItem == 0) return;
        Point p = e.Location;
        float d = (float)Math.Sqrt((p.X - lastM.X) * (p.X - lastM.X) + (p.Y - lastM.Y) * (p.Y - lastM.Y));
        lastM = p;
        int i = dragItem;
        ix[i] = Math.Max(0, Math.Min(Width - iw[i], p.X - dox));
        iy[i] = Math.Max(0, Math.Min(Height - ih[i], p.Y - doy));
        bool over = ItemRect(i).IntersectsWith(PetRect());
        if (i == 1 && over)   // 肥皂搓澡
        {
            rub += d;
            if (tick % 2 == 0) Add(5, ix[1] + Rnd() * iw[1], iy[1], (Rnd() - 0.5f) * K, -(0.5f + Rnd()) * K, 25);
            while (rub >= 26f * K)
            {
                rub -= 26f * K;
                if (foam < 12)
                {
                    foam++;
                    if (foam == 12 && !scrubbed) { scrubbed = true; }
                    if (foam == 12 && !saidFoam) { saidFoam = true; pet.BathSay("好多泡泡!", "So bubbly!"); }
                }
            }
        }
        if (i == 3 && over)   // 浴巾擦干
        {
            if (!clean)
            {
                if (!saidTowel) { saidTowel = true; pet.BathSay("还没洗干净~", "Not clean yet~"); }
            }
            else if (wet)
            {
                dry += d;
                if (tick % 3 == 0) Add(4, ix[3] + Rnd() * iw[3], iy[3] + ih[3], (Rnd() - 0.5f) * 2f * K, 0, 10);
                if (dry >= 420f * K) { wet = false; dried = true; Finish(); }
            }
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (dragItem == 0) return;
        ix[dragItem] = hx[dragItem]; iy[dragItem] = hy[dragItem];
        dragItem = 0;
        Invalidate();
    }

    // ---------- 绘制 ----------
    void FR(Graphics g, Brush b, float x, float y, float w, float h)
    {
        int x0 = (int)Math.Round(x * SZ), y0 = (int)Math.Round(y * SZ);
        g.FillRectangle(b, x0, y0, (int)Math.Round((x + w) * SZ) - x0, (int)Math.Round((y + h) * SZ) - y0);
    }

    protected override void OnPaint(PaintEventArgs e) { DrawBath(e.Graphics); }

    internal void DrawBath(Graphics g)
    {
        int u = Math.Max(1, SZ / 4);
        using (Brush silver = new SolidBrush(Color.FromArgb(185, 192, 205)))
        using (Brush silverDark = new SolidBrush(Color.FromArgb(130, 138, 152)))
        using (Brush tubBack = new SolidBrush(Color.FromArgb(222, 236, 248)))
        using (Brush tubWhite = new SolidBrush(Color.FromArgb(250, 251, 253)))
        using (Brush tubLine = new SolidBrush(Color.FromArgb(185, 200, 218)))
        using (Brush gold = new SolidBrush(Color.FromArgb(235, 190, 70)))
        using (Brush water = new SolidBrush(Color.FromArgb(150, 110, 185, 240)))
        using (Brush waterTop = new SolidBrush(Color.FromArgb(210, 190, 228, 255)))
        using (Brush foamB = new SolidBrush(Color.FromArgb(252, 252, 255)))
        using (Brush foamShade = new SolidBrush(Color.FromArgb(205, 225, 245)))
        using (Brush wood = new SolidBrush(Color.FromArgb(190, 140, 95)))
        using (Brush woodDark = new SolidBrush(Color.FromArgb(150, 105, 70)))
        using (Brush drop = new SolidBrush(Color.FromArgb(120, 190, 255)))
        {
            // 花洒杆子
            FR(g, silver, 32f, 1.6f, 0.5f, 18.4f);
            FR(g, silver, 21.5f, 1.6f, 11f, 0.5f);
            using (Brush hot = new SolidBrush(Color.FromArgb(230, 90, 90)))
            using (Brush cold = new SolidBrush(Color.FromArgb(90, 150, 240)))
            { FR(g, hot, 31.5f, 10f, 0.7f, 0.8f); FR(g, cold, 32.3f, 10f, 0.7f, 0.8f); }
            FR(g, silverDark, 31.6f, 5.6f, 1.0f, 0.4f);   // 挂浴巾的钩子

            // 浴缸后壁
            FR(g, tubBack, 10, 11, 18, 7);
            FR(g, tubWhite, 10, 11, 18, 0.4f);

            // Claude（只有真的跳进浴缸以后才画，避免跳进来之前出现两只）
            bool showPet = pet.InBath || ShowPetAlways;
            bool closed = spraying || finishT > 0 || (dragItem == 1 && ItemRect(1).IntersectsWith(PetRect())) || tick % 100 < 4;
            if (showPet)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(PetCol * SZ, PetRow * SZ);
                pet.DrawBather(g, SZ, pet.Outfit, closed);
                g.Restore(st);
            }

            // 身上的泡沫
            for (int i = 0; showPet && i < foam && i < 12; i++)
            {
                float cx = FoamPos[i, 0], cy = FoamPos[i, 1], r = FoamPos[i, 2];
                g.FillEllipse(foamShade, (int)((cx - r) * SZ), (int)((cy - r + 0.15f) * SZ), (int)(2 * r * SZ), (int)(2 * r * SZ));
                g.FillEllipse(foamB, (int)((cx - r) * SZ), (int)((cy - r) * SZ), (int)(2 * r * SZ), (int)(2 * r * SZ));
            }
            // 湿漉漉的水珠
            if (showPet && wet && foam == 0)
            {
                FR(g, drop, 14.3f, 13.2f, 0.3f, 0.5f);
                FR(g, drop, 23.4f, 13.8f, 0.3f, 0.5f);
                FR(g, drop, 18.6f, 11.6f, 0.3f, 0.5f);
                FR(g, drop, 21.0f, 12.1f, 0.3f, 0.5f);
            }

            // 水
            float wt = WaterTop();
            if (level > 0.01f)
            {
                g.FillRectangle(water, 10 * SZ, (int)wt, 18 * SZ, (int)(18 * SZ - wt));
                g.FillRectangle(waterTop, 10 * SZ, (int)wt, 18 * SZ, Math.Max(1, u));
                int nb = (int)(bubbly * 16);
                for (int i = 0; i < nb; i++)
                {
                    float bx = 10.4f + ((i * 7) % 17) + (i % 2) * 0.4f;
                    float r = (0.55f + (i % 3) * 0.3f) * SZ;
                    g.FillEllipse(foamShade, bx * SZ, wt - r * 0.55f + u / 2, r, r);
                    g.FillEllipse(foamB, bx * SZ, wt - r * 0.6f, r, r);
                }
            }

            // 浴缸前壁
            FR(g, tubWhite, 9, 16.5f, 20, 3.1f);
            FR(g, tubLine, 9, 16.5f, 20, 0.35f);
            FR(g, tubLine, 9.4f, 19.2f, 19.2f, 0.4f);
            FR(g, tubLine, 9, 16.5f, 0.2f, 3.1f);
            FR(g, tubLine, 28.8f, 16.5f, 0.2f, 3.1f);
            FR(g, gold, 10, 19.6f, 1.4f, 0.4f);
            FR(g, gold, 26.6f, 19.6f, 1.4f, 0.4f);

            // 花洒头
            FR(g, silverDark, 17, 2.1f, 5, 1.3f);
            FR(g, silver, 17.2f, 2.2f, 4.6f, 1.0f);
            using (Brush holes = new SolidBrush(spraying ? Color.FromArgb(120, 190, 255) : Color.FromArgb(90, 95, 110)))
                for (float hxx = 17.5f; hxx < 21.6f; hxx += 0.8f) FR(g, holes, hxx, 3.3f, 0.35f, 0.3f);

            // 小板凳 + 肥皂盒
            FR(g, woodDark, 0.4f, 16.4f, 0.5f, 3.6f);
            FR(g, woodDark, 4.1f, 16.4f, 0.5f, 3.6f);
            FR(g, wood, 0, 15.9f, 5, 0.6f);
            using (Brush dish = new SolidBrush(Color.FromArgb(160, 205, 235)))
                FR(g, dish, 0.2f, 15.3f, 2.8f, 0.6f);

            // 物品（拖着的那个最后画）
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i <= 3; i++)
                {
                    bool isDrag = i == dragItem;
                    if ((pass == 0) == isDrag) continue;
                    if (i == 1) DrawSoap(g);
                    if (i == 2) DrawBottle(g);
                    if (i == 3) DrawTowel(g);
                }

            // 粒子
            foreach (BP p in ps)
            {
                int x = (int)p.X, y = (int)p.Y;
                switch (p.Kind)
                {
                    case 0: g.FillRectangle(drop, x, y, Math.Max(1, u), SZ / 2); break;
                    case 1: using (Brush b = new SolidBrush(Color.FromArgb(200, 165, 245))) g.FillRectangle(b, x, y, Math.Max(2, u + 1), Math.Max(2, u + 1)); break;
                    case 2:
                        using (Brush b = new SolidBrush(p.Life % 6 < 3 ? Color.White : Color.FromArgb(255, 215, 80)))
                        {
                            g.FillRectangle(b, x, y - u, u, 3 * u);
                            g.FillRectangle(b, x - u, y, 3 * u, u);
                        }
                        break;
                    case 3:
                        using (Pen pen = new Pen(Color.FromArgb(150, 200, 245)))
                            g.DrawEllipse(pen, x, y, SZ * 0.7f, SZ * 0.7f);
                        g.FillRectangle(Brushes.White, x + SZ / 5, y + SZ / 5, Math.Max(1, u / 2 + 1), Math.Max(1, u / 2 + 1));
                        break;
                    case 4: g.FillRectangle(drop, x, y, Math.Max(1, u), Math.Max(1, u)); break;
                    case 5: g.FillEllipse(foamB, x, y, SZ / 2, SZ / 2); break;
                }
            }
        }
        DrawHints(g);
    }

    void DrawSoap(Graphics g)
    {
        float x = ix[1], y = iy[1], w = iw[1], h = ih[1];
        using (Brush s = new SolidBrush(Color.FromArgb(150, 220, 195)))
        using (Brush sd = new SolidBrush(Color.FromArgb(105, 180, 155)))
        using (Brush sl = new SolidBrush(Color.FromArgb(205, 245, 230)))
        {
            g.FillRectangle(sd, x, y + h * 0.2f, w, h * 0.8f);
            g.FillRectangle(s, x + w * 0.08f, y, w * 0.84f, h * 0.8f);
            g.FillRectangle(sl, x + w * 0.2f, y + h * 0.15f, w * 0.35f, h * 0.2f);
        }
    }

    void DrawBottle(Graphics g)
    {
        float x = ix[2], y = iy[2], w = iw[2], h = ih[2];
        GraphicsState st = g.Save();
        if (Pouring())
        {
            g.TranslateTransform(x + w / 2, y + h / 2);
            g.RotateTransform(180);
            g.TranslateTransform(-(x + w / 2), -(y + h / 2));
        }
        using (Brush body = new SolidBrush(Color.FromArgb(160, 120, 220)))
        using (Brush bodyDark = new SolidBrush(Color.FromArgb(125, 90, 185)))
        using (Brush pump = new SolidBrush(Color.FromArgb(80, 80, 95)))
        using (Brush dots = new SolidBrush(Color.FromArgb(200, 180, 245)))
        {
            g.FillRectangle(pump, x + w * 0.35f, y, w * 0.3f, h * 0.2f);
            g.FillRectangle(pump, x + w * 0.35f, y, w * 0.65f, h * 0.06f);
            g.FillRectangle(bodyDark, x, y + h * 0.2f, w, h * 0.8f);
            g.FillRectangle(body, x, y + h * 0.2f, w * 0.8f, h * 0.8f);
            g.FillRectangle(Brushes.White, x + w * 0.12f, y + h * 0.45f, w * 0.76f, h * 0.3f);
            g.FillRectangle(dots, x + w * 0.25f, y + h * 0.52f, w * 0.18f, w * 0.18f);
            g.FillRectangle(dots, x + w * 0.55f, y + h * 0.6f, w * 0.18f, w * 0.18f);
        }
        g.Restore(st);
    }

    void DrawTowel(Graphics g)
    {
        float x = ix[3], y = iy[3], w = iw[3], h = ih[3];
        using (Brush t = new SolidBrush(Color.FromArgb(245, 160, 185)))
        using (Brush td = new SolidBrush(Color.FromArgb(220, 125, 155)))
        {
            g.FillRectangle(td, x, y, w, h);
            g.FillRectangle(t, x, y, w * 0.85f, h);
            g.FillRectangle(Brushes.White, x, y + h * 0.7f, w, h * 0.08f);
            g.FillRectangle(Brushes.White, x, y + h * 0.82f, w, h * 0.05f);
            g.FillRectangle(td, x, y, w, h * 0.12f);
        }
    }

    void DrawHints(Graphics g)
    {
        // 只显示当前这一步 + 一句小提示，避免挡住花洒
        bool en = pet.En;
        string[] steps = en
            ? new[] { "Click the shower", "Scrub with the soap", "Shower to rinse", "Dry with the towel", "All done!" }
            : new[] { "点花洒放水", "用肥皂给它搓一搓", "打开花洒冲干净", "用浴巾擦干身体", "洗好啦!" };
        int cur = level < 0.5f ? 0 : (!scrubbed ? 1 : (!clean ? 2 : (!dried ? 3 : 4)));
        string[] lines = {
            (cur < 4 ? "(" + (cur + 1) + "/4) " : "") + steps[cur],
            en ? "Tip: body wash = bubbles" : "沐浴露倒进水里会起泡泡" };
        bool[] done = { cur == 4, false, false, false };
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        float lh = hintFont.GetHeight(g) + 1, w = 0;
        foreach (string s in lines) w = Math.Max(w, g.MeasureString(s, hintFont).Width);
        float x0 = 0.3f * SZ, y0 = 0.3f * SZ, pw = w + 8, phh = lh * lines.Length + 6;
        using (Brush bg = new SolidBrush(Color.FromArgb(255, 255, 255)))
        using (Pen border = new Pen(Color.FromArgb(190, 205, 225)))
        {
            g.FillRectangle(bg, x0, y0, pw, phh);
            g.DrawRectangle(border, x0, y0, pw, phh);
        }
        using (Brush normal = new SolidBrush(Color.FromArgb(60, 70, 90)))
        using (Brush ok = new SolidBrush(Color.FromArgb(50, 160, 90)))
        using (Brush tip = new SolidBrush(Color.FromArgb(150, 120, 210)))
            for (int i = 0; i < lines.Length; i++)
            {
                bool isTip = i == 1, isDone = !isTip && done[i];
                g.DrawString(lines[i], hintFont, isTip ? tip : (isDone ? ok : normal), x0 + 4, y0 + 3 + i * lh);
            }
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
    }
}

// ---------- 玩具：足球 / 毛线球，有重力和弹跳，可以拖着扔 ----------
class ToyForm : Form
{
    // Kind: 0 足球, 1 毛线球, 2 气球, 3 飞盘, 4 泡泡, 5 激光笔红点
    readonly PetForm pet;
    internal readonly int Kind;
    readonly int fp; readonly float K;
    internal float x, y, vx, vy, angle;
    internal bool held, kickedByPet, carried, landed, poppedByClick;
    float wob;
    bool moved; float offX, offY; Point down, lastM;
    readonly Random rand = new Random();
    static readonly string[] Mask = { "..####..", ".######.", "########", "########", "########", "########", ".######.", "..####.." };

    static readonly string[][] Arts = {
        null, null,
        new[] { "..RRRR..", ".RLRRRR.", "RLRRRRRR", "RRRRRRRR", ".RRRRRR.", "..RRRR..", "...KK...", "....S..." },   // 气球
        new[] { "........", "........", "..BBBB..", "BBLLBBBB", "BBBBBBBB", ".DDDDDD.", "........", "........" },   // 飞盘
        new[] { "..CCCC..", ".CWW..C.", "CW.....C", "C......C", "C......C", "C......C", ".C....C.", "..CCCC.." },   // 泡泡
        new[] { ".PP.", "PRRP", "PRRP", ".PP." },                                                                // 激光点
    };
    static readonly Dictionary<char, Color> ArtCols = new Dictionary<char, Color> {
        { 'R', Color.FromArgb(235, 70, 95) }, { 'L', Color.FromArgb(255, 160, 175) }, { 'K', Color.FromArgb(170, 40, 60) },
        { 'S', Color.FromArgb(150, 150, 160) }, { 'B', Color.FromArgb(70, 150, 235) }, { 'D', Color.FromArgb(40, 100, 180) },
        { 'C', Color.FromArgb(130, 195, 240) }, { 'W', Color.White }, { 'P', Color.FromArgb(255, 150, 160) },
    };

    public ToyForm(PetForm pet, int kind, int fp, float k)
    {
        this.pet = pet; Kind = kind; this.fp = fp; K = k;
        Text = "Claude Toy";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(255, 0, 255);
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        int n = kind == 5 ? 4 : 8;
        ClientSize = new Size(n * fp, n * fp);
    }
    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08000000;
            if (Kind == 5) cp.ExStyle |= 0x20;   // 激光点：鼠标点击直接穿过去
            return cp;
        }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    internal void Sync() { Location = new Point((int)x, (int)y); }
    Rectangle WA() { return Screen.FromPoint(new Point((int)(x + Width / 2), (int)(y + Height / 2))).WorkingArea; }
    internal bool NearGround(float tol) { return y >= WA().Bottom - Height - tol; }

    internal void Kick(int dir)
    {
        kickedByPet = true;
        double r1 = rand.NextDouble(), r2 = rand.NextDouble();
        if (Kind == 0) { vx = dir * (float)(6 + r1 * 7) * K; vy = -(float)(4 + r2 * 8) * K; }
        else { vx = dir * (float)(3 + r1 * 4) * K; vy = -(float)(2 + r2 * 4) * K; }
    }
    // 气球被顶一下
    internal void Bump(int dir)
    {
        double r1 = rand.NextDouble(), r2 = rand.NextDouble();
        vy = -(float)(7 + r2 * 3) * K; vx = dir * (float)(0.5 + r1 * 2.5) * K; landed = false;
    }

    // 返回 1 进球, 2 气球掉地上, 3 泡泡飞走了
    internal int Step()
    {
        if (held || carried) return 0;
        Rectangle wa = WA();
        float ground = wa.Bottom - Height;
        int ev = 0;
        switch (Kind)
        {
            case 5:   // 红点跟着鼠标，贴在地面上
                {
                    Point c = Cursor.Position;
                    Rectangle sw = Screen.FromPoint(c).WorkingArea;
                    x = Math.Max(sw.Left, Math.Min(sw.Right - Width, c.X - Width / 2));
                    y = sw.Bottom - Height;
                    Sync(); Invalidate();
                    return 0;
                }
            case 4:   // 泡泡慢慢往上飘
                wob += 0.09f;
                x += (float)Math.Sin(wob) * 0.9f * K;
                y -= 0.8f * K;
                if (y < wa.Top) ev = 3;
                Sync();
                return ev;
            case 2:   // 气球：很轻，慢慢落下
                vy = Math.Min(vy + 0.07f * K, 2.2f * K);
                vx *= 0.985f;
                x += vx; y += vy;
                if (y >= ground) { y = ground; vy = 0; vx *= 0.9f; if (!landed) { landed = true; ev = 2; } }
                angle += 0.06f;
                break;
            case 3:   // 飞盘：飞得快的时候几乎不往下掉
                vy += Math.Abs(vx) > 2.5f * K ? 0.12f * K : 0.7f * K;
                vx *= 0.988f;
                x += vx; y += vy;
                if (y >= ground) { y = ground; vy = 0; vx *= 0.85f; if (Math.Abs(vx) < 0.15f) vx = 0; }
                break;
            default:  // 足球 / 毛线球
                {
                    float bounce = Kind == 0 ? 0.6f : 0.3f;
                    vy += 0.8f * K; x += vx; y += vy;
                    if (y >= ground)
                    {
                        y = ground;
                        if (vy > 2.5f * K) vy = -vy * bounce; else vy = 0;
                        vx *= Kind == 0 ? 0.97f : 0.92f;
                        if (Math.Abs(vx) < 0.15f) vx = 0;
                    }
                    else vx *= 0.995f;
                    angle += vx / (Width / 2f);
                }
                break;
        }
        if (y < wa.Top) { y = wa.Top; vy = -vy * 0.5f; }
        if (x < wa.Left || x > wa.Right - Width)
        {
            if (kickedByPet && Kind == 0 && Math.Abs(vx) > 4f * K) ev = 1;
            x = x < wa.Left ? wa.Left : wa.Right - Width;
            vx = -vx * 0.7f; kickedByPet = false;
        }
        Sync();
        Invalidate();
        return ev;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (Kind == 4) { poppedByClick = true; return; }   // 点泡泡 = 戳破
        Point c = Cursor.Position;
        held = true; moved = false; kickedByPet = false; carried = false; landed = false;
        offX = c.X - x; offY = c.Y - y; down = c; lastM = c; vx = 0; vy = 0;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!held) return;
        Point c = Cursor.Position;
        if (!moved && Math.Abs(c.X - down.X) + Math.Abs(c.Y - down.Y) > 3) moved = true;
        if (!moved) return;
        vx = (c.X - lastM.X) * 0.7f + vx * 0.3f; vy = (c.Y - lastM.Y) * 0.7f + vy * 0.3f;
        lastM = c;
        x = c.X - offX; y = c.Y - offY;
        Sync();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !held) return;
        held = false;
        if (!moved) { vy = (Kind == 2 ? -6f : -9f) * K; vx = (float)(rand.NextDouble() - 0.5) * 8f * K; }   // 点一下：弹起来
        float lim = 30f * K;
        vx = Math.Max(-lim, Math.Min(lim, vx)); vy = Math.Max(-lim, Math.Min(lim, vy));
    }

    protected override void OnPaint(PaintEventArgs e) { DrawToy(e.Graphics, Kind, fp, angle); }

    static bool In(int c, int r) { return r >= 0 && r < 8 && c >= 0 && c < 8 && Mask[r][c] == '#'; }

    internal static void DrawToy(Graphics g, int kind, int fp, float angle)
    {
        if (kind >= 2)
        {
            string[] rows = Arts[kind];
            int dx = kind == 2 ? (int)Math.Round(Math.Sin(angle) * fp / 2) : 0;   // 气球轻轻左右晃
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    char ch = rows[r][c];
                    if (ch == '.') continue;
                    using (Brush b = new SolidBrush(ArtCols[ch]))
                        g.FillRectangle(b, c * fp + (r < 6 ? dx : 0), r * fp, fp, fp);
                }
            return;
        }
        Color fill = kind == 0 ? Color.FromArgb(250, 250, 250) : Color.FromArgb(240, 130, 165);
        Color edge = kind == 0 ? Color.FromArgb(150, 150, 160) : Color.FromArgb(190, 75, 115);
        Color mark = kind == 0 ? Color.FromArgb(35, 35, 40) : Color.FromArgb(205, 90, 130);
        bool[,] marks = new bool[8, 8];
        if (kind == 0)
        {
            int cx = (int)Math.Round(3.5 + 0.9 * Math.Cos(angle) - 1), cy = (int)Math.Round(3.5 + 0.9 * Math.Sin(angle) - 1);
            for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++) if (In(cx + a, cy + b)) marks[cx + a, cy + b] = true;
            for (int k = 0; k < 5; k++)
            {
                double t = angle + k * Math.PI * 2 / 5;
                int mx = (int)Math.Floor(3.5 + 2.9 * Math.Cos(t)), my = (int)Math.Floor(3.5 + 2.9 * Math.Sin(t));
                if (In(mx, my)) marks[mx, my] = true;
            }
        }
        else
        {
            foreach (double off in new[] { -1.6, 0.0, 1.6 })
                for (double s = -4; s <= 4; s += 0.5)
                {
                    double t = angle;
                    int mx = (int)Math.Floor(3.5 + s * Math.Cos(t) - off * Math.Sin(t));
                    int my = (int)Math.Floor(3.5 + s * Math.Sin(t) + off * Math.Cos(t));
                    if (In(mx, my)) marks[mx, my] = true;
                }
        }
        using (Brush bf = new SolidBrush(fill))
        using (Brush be = new SolidBrush(edge))
        using (Brush bm = new SolidBrush(mark))
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    if (!In(c, r)) continue;
                    bool isEdge = !In(c - 1, r) || !In(c + 1, r) || !In(c, r - 1) || !In(c, r + 1);
                    Brush br = isEdge ? be : (marks[c, r] ? bm : bf);
                    g.FillRectangle(br, c * fp, r * fp, fp, fp);
                }
            if (kind == 1) g.FillRectangle(Brushes.White, 2 * fp, 2 * fp, fp, fp);   // 高光
        }
    }
}

// ---------- 设置窗口：语言 + 装扮 + 小窝（小窝的装饰和玩偶都在小窝页里，用滚轮上下看） ----------
// 灯光层：天黑时把背景压暗，小窝和落地灯周围亮起暖光；Claude 走到附近就会被照到
class LightForm : Form
{
    internal const int LC = 48, LR = 28;   // 比背景大一圈
    internal static readonly Color[] GlowColors = {
        Color.FromArgb(255, 206, 96), Color.FromArgb(255, 160, 70), Color.FromArgb(255, 150, 190), Color.FromArgb(130, 180, 255),
        Color.FromArgb(140, 230, 150), Color.FromArgb(190, 150, 255), Color.FromArgb(255, 245, 225) };
    internal static readonly string[] GlowZh = { "暖黄", "橘色", "粉色", "蓝色", "绿色", "紫色", "白色" };
    internal static readonly string[] GlowEn = { "Warm yellow", "Orange", "Pink", "Blue", "Green", "Purple", "White" };
    readonly int SZ;
    string key;
    Bitmap bmp;
    byte alpha = 255;
    internal void Fade(double op)
    {
        byte a = (byte)Math.Max(0, Math.Min(255, (int)(op * 255)));
        if (a == alpha) return;
        alpha = a;
        if (bmp != null) Push(Left, Top);
    }

    [StructLayout(LayoutKind.Sequential)] struct PT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct SZS { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte op, flags, alpha, fmt; }
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);
    [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref PT pos, ref SZS size, IntPtr src, ref PT srcPos, int key, ref BLEND bl, int flags);

    public LightForm(int sz)
    {
        SZ = sz;
        Text = "Claude Light";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(LC * sz, LR * sz);
    }
    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x80000; return cp; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }

    internal void Render(NestLook L, float dk, bool lamp, int twinkle, int x, int y)
    {
        bool fairy = L.Bg == 1 && (L.RoomDecor & 128) != 0 && dk > 0;
        string k = L.Bg + "," + dk + "," + lamp + "," + (fairy ? twinkle : -1) + "," + L.GlowAlpha + "," + L.GlowSpread + "," + L.GlowSize + "," + L.GlowColor + "," + L.GlowOn;
        if (k == key && bmp != null)
        {
            if (Left != x || Top != y) Push(x, y);
            return;
        }
        key = k;
        if (bmp != null) bmp.Dispose();
        bmp = Build(SZ, L, dk, lamp, twinkle);
        Push(x, y);
    }

    void Push(int x, int y)
    {
        IntPtr screen = GetDC(IntPtr.Zero), mem = CreateCompatibleDC(screen);
        IntPtr hb = bmp.GetHbitmap(Color.FromArgb(0)), old = SelectObject(mem, hb);
        PT dst = new PT(); dst.x = x; dst.y = y;
        PT src = new PT();
        SZS size = new SZS(); size.cx = bmp.Width; size.cy = bmp.Height;
        BLEND bl = new BLEND(); bl.op = 0; bl.alpha = alpha; bl.fmt = 1;
        UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref bl, 2);
        SelectObject(mem, old); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen);
    }

    // 一格一格算光：离光源越近越亮（分成几圈，保持像素感），远处被夜色压暗
    internal static Bitmap Build(int sz, NestLook L, float dk, bool lamp, int twinkle)
    {
        int W = LC * sz, H = LR * sz, blk = Math.Max(2, sz / 2);
        float ox = (LC - BackdropForm.BC) / 2f, oy = LR - BackdropForm.BR;   // 背景在灯光层里的位置
        List<float[]> src = new List<float[]>();   // x, y, 半径, 强度, r, g, b, 中间全亮的比例
        if (dk > 0 && L.GlowOn)   // 小窝的主灯光：大小、扩散、透明度、颜色都能调
        {
            Color gc = GlowColors[L.GlowColor];
            src.Add(new float[] { LC / 2f, LR - 7f, 5 + L.GlowSize * 0.14f, 0.2f + L.GlowAlpha * 0.008f, gc.R, gc.G, gc.B, 0.7f - L.GlowSpread * 0.007f });
        }                      // 小窝周围的暖光
        if (lamp) src.Add(new float[] { ox + 36.6f, oy + 10f, 13, 1.0f, 255, 214, 110 });                   // 落地灯
        if (L.Bg == 1 && (L.RoomDecor & 128) != 0 && dk > 0)
        {
            Color[] lc = { Color.FromArgb(255, 110, 110), Color.FromArgb(255, 210, 90), Color.FromArgb(120, 210, 130), Color.FromArgb(110, 170, 255) };
            for (int i = 0; i < 20; i++)
                if ((twinkle + i) % 3 != 0) src.Add(new float[] { ox + i * 2 + 1f, oy + 1.7f, 1.8f, 0.7f, lc[i % 4].R, lc[i % 4].G, lc[i % 4].B });
        }
        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        int[] px = new int[W * H];
        for (int by = 0; by < H; by += blk)
            for (int bx = 0; bx < W; bx += blk)
            {
                float cx = (bx + blk / 2f) / sz, cy = (by + blk / 2f) / sz;
                float lit = 0, cr = 0, cg = 0, cb = 0;
                foreach (float[] s in src)
                {
                    float d = (float)Math.Sqrt((cx - s[0]) * (cx - s[0]) + (cy - s[1]) * (cy - s[1]));
                    if (d >= s[2]) continue;
                    float core = s.Length > 7 ? s[7] * s[2] : 0;   // 中间这一圈是全亮的，外面慢慢暗下去
                    float t2 = d <= core ? 1 : 1 - (d - core) / (s[2] - core);
                    float f = (float)Math.Ceiling(t2 * 4) / 4 * s[3];
                    lit += f; cr += s[4] * f; cg += s[5] * f; cb += s[6] * f;
                }
                // 夜色覆盖范围：有背景就盖住背景（边缘慢慢淡出），没有背景就只在小窝附近
                float cov;
                if (L.Bg > 0)
                {
                    float dx = Math.Max(Math.Max(ox - cx, cx - (ox + BackdropForm.BC)), 0), dy = Math.Max(oy - cy, 0);
                    cov = Math.Max(0, 1 - (float)Math.Sqrt(dx * dx + dy * dy) / 4);
                }
                else
                {
                    float d = (float)Math.Sqrt((cx - LC / 2f) * (cx - LC / 2f) + (cy - (LR - 6f)) * (cy - (LR - 6f)));
                    cov = Math.Max(0, 1 - d / 16) * 0.8f;
                }
                cov = (float)Math.Ceiling(cov * 4) / 4;
                float lc2 = Math.Min(1, lit);
                // 光圈里：夜色退掉，换成暖黄色；越靠近光源越黄（不再和夜色混成灰色）
                float ad = dk * cov * (1 - lc2) * (1 - lc2);
                float aw = lc2 * (0.34f + 0.12f * dk);
                float a = Math.Min(0.62f, ad + aw);
                a = (float)Math.Round(a * 16) / 16;
                if (a <= 0.01f) continue;
                float wr = lit > 0 ? cr / lit : 0, wg = lit > 0 ? cg / lit : 0, wb = lit > 0 ? cb / lit : 0;
                float tw = (float)Math.Sqrt(lc2);
                int r = (int)Math.Min(255, 12 + (wr - 12) * tw), g = (int)Math.Min(255, 16 + (wg - 16) * tw), bl = (int)Math.Min(255, 42 + (wb - 42) * tw);                int argb = ((int)(a * 255) << 24) | (r << 16) | (g << 8) | bl;
                for (int yy = by; yy < Math.Min(H, by + blk); yy++)
                    for (int xx = bx; xx < Math.Min(W, bx + blk); xx++) px[yy * W + xx] = argb;
            }
        BitmapData bd = b.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        Marshal.Copy(px, 0, bd.Scan0, px.Length);
        b.UnlockBits(bd);
        return b;
    }
}
// 套餐：挑最多 3 样食物放到餐盘上，端给 Claude
// 小窝后面的背景：温馨小屋（带窗户）/ 草地 / 星空，会下雨、打雷、下雪
class BackdropForm : Form
{
    internal const int BC = 40, BR = 24;
    readonly PetForm pet;
    readonly int SZ;

    internal static readonly string[] SceneZh = { "无", "温馨小屋", "草地", "星空" };
    internal static readonly string[] SceneEn = { "None", "Cozy room", "Meadow", "Starry night" };
    internal static readonly Color[] WallColors = {
        Color.FromArgb(238, 214, 190), Color.FromArgb(200, 222, 242), Color.FromArgb(246, 206, 216),
        Color.FromArgb(204, 230, 200), Color.FromArgb(222, 208, 240), Color.FromArgb(88, 96, 128) };
    internal static readonly string[] WallZh = { "奶油", "天蓝", "樱花粉", "薄荷绿", "薰衣草", "夜空蓝" };
    internal static readonly string[] WallEn = { "Cream", "Sky", "Sakura", "Mint", "Lavender", "Midnight" };
    internal static readonly string[] RoomZh = { "窗帘", "挂画", "书架", "落地灯", "地毯", "挂钟", "大盆栽", "小彩灯" };
    internal static readonly string[] RoomEn = { "Curtains", "Painting", "Bookshelf", "Floor lamp", "Rug", "Clock", "Big plant", "Fairy lights" };
    internal static readonly string[] OutZh = { "郁金香", "小雏菊", "向日葵", "蘑菇", "蝴蝶", "木栅栏", "小树", "草丛", "萤火虫" };
    internal static readonly string[] OutEn = { "Tulips", "Daisies", "Sunflowers", "Mushrooms", "Butterflies", "Fence", "Tree", "Bushes", "Fireflies" };
    internal static readonly string[] WeatherZh = { "自动", "晴天", "下雨", "打雷", "下雪" };
    internal static readonly string[] WeatherEn = { "Auto", "Sunny", "Rain", "Thunder", "Snow" };

    public BackdropForm(PetForm pet, int sz)
    {
        this.pet = pet; SZ = sz;
        Text = "Claude Backdrop";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(255, 0, 255);
        TransparencyKey = BackColor;
        DoubleBuffered = true;
        ClientSize = new Size(BC * sz, BR * sz);
    }
    protected override CreateParams CreateParams
    {
        // 不进 Alt+Tab、不抢焦点、鼠标穿透（点背景就像点桌面）
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x80000; return cp; }
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override void OnPaint(PaintEventArgs e) { Draw(e.Graphics, SZ, pet.Look, pet.Weather, pet.Tick); }

    // 打雷：不规则地闪一下
    internal static int FlashFrom = -999, FlashTo = -999;   // Claude 那边随机安排的闪电时间
    internal static bool Flash(int tick) { return tick >= FlashFrom && tick < FlashTo; }
    static bool BoltNow(int tick) { return tick >= FlashTo && tick < FlashTo + 6; }

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    bool clickable;
    internal void SetClickable(bool on)
    {
        if (on == clickable || !IsHandleCreated) return;
        clickable = on;
        int ex = GetWindowLong(Handle, -20);
        SetWindowLong(Handle, -20, on ? ex & ~0x20 : ex | 0x20);
        Cursor = on ? Cursors.Hand : Cursors.Default;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && clickable) pet.ToggleLamp();
    }
    // 点它的时候不要被系统提到最前面
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; }   // WM_MOUSEACTIVATE → MA_NOACTIVATE
        base.WndProc(ref m);
    }

    static void F(Graphics g, Brush b, int s, float x, float y, float w, float h)
    {
        int x0 = (int)Math.Round(x * s), y0 = (int)Math.Round(y * s);
        int x1 = (int)Math.Round((x + w) * s), y1 = (int)Math.Round((y + h) * s);
        if (x1 > x0 && y1 > y0) g.FillRectangle(b, x0, y0, x1 - x0, y1 - y0);
    }
    static Brush B(int r, int g, int b) { return new SolidBrush(Color.FromArgb(r, g, b)); }
    static Color SkyOf(int weather, bool night)
    {
        if (night) return weather >= 2 ? Color.FromArgb(28, 30, 50) : Color.FromArgb(32, 38, 78);
        switch (weather)
        {
            case 2: return Color.FromArgb(128, 142, 162);
            case 3: return Color.FromArgb(78, 82, 104);
            case 4: return Color.FromArgb(196, 206, 222);
            default: return Color.FromArgb(150, 205, 250);
        }
    }

    internal static void Draw(Graphics g, int s, NestLook L, int weather, int tick)
    {
        if (L.Bg == 0) return;
        if (L.Bg == 1) DrawRoom(g, s, L, weather, tick);
        else DrawOutdoor(g, s, L, weather, tick, L.Bg == 3);
    }

    // 天气粒子：雨、雪（在给定的格子范围里）
    static void Weather(Graphics g, int s, int weather, int tick, float x0, float y0, float w, float h, int n)
    {
        if (weather == 2 || weather == 3)
        {
            using (Brush drop = B(190, 210, 240))
                for (int i = 0; i < n; i++)
                {
                    float x = x0 + ((i * 37) % 100) / 100f * w;
                    float y = y0 + ((tick * 0.45f + i * 7.3f) % h);
                    F(g, drop, s, x, y, 0.15f, 0.7f);
                }
        }
        else if (weather == 4)
        {
            for (int i = 0; i < n; i++)
            {
                float x = x0 + ((i * 41) % 100) / 100f * w + (float)Math.Sin((tick + i * 20) / 18.0) * 0.4f;
                float y = y0 + ((tick * 0.08f + i * 5.1f) % h);
                F(g, Brushes.White, s, x, y, 0.3f, 0.3f);
            }
        }
    }
    static void Bolt(Graphics g, int s, float x, float y)
    {
        using (Brush yb = B(255, 236, 120))
        {
            F(g, yb, s, x, y, 0.5f, 1.2f); F(g, yb, s, x - 0.5f, y + 1.2f, 0.5f, 1.2f);
            F(g, yb, s, x, y + 2.4f, 0.5f, 1.2f); F(g, yb, s, x - 0.5f, y + 3.6f, 0.5f, 1f);
        }
    }

    static void DrawRoom(Graphics g, int s, NestLook L, int weather, int tick)
    {
        int D = L.RoomDecor;
        Color wc = WallColors[L.Wall];
        using (Brush wall = new SolidBrush(wc))
        using (Brush stripe = new SolidBrush(NestForm.Mix(wc, Color.White, 0.25f)))
        using (Brush wallDark = new SolidBrush(NestForm.Mix(wc, Color.Black, 0.2f)))
        using (Brush wood = B(196, 146, 100))
        using (Brush woodDark = B(165, 118, 78))
        using (Brush white = B(250, 248, 242))
        using (Brush brown = B(130, 88, 58))
        {
            F(g, wall, s, 0, 0, 40, 20);
            for (int x = 2; x < 40; x += 4) F(g, stripe, s, x, 0, 1, 20);
            F(g, wallDark, s, 0, 19.4f, 40, 0.6f);
            F(g, wood, s, 0, 20, 40, 4);
            F(g, woodDark, s, 0, 21.9f, 40, 0.2f);
            for (int x = 0; x < 40; x += 5) { F(g, woodDark, s, x + 0.0f, 20, 0.2f, 1.9f); F(g, woodDark, s, x + 2.5f, 22.1f, 0.2f, 1.9f); }

            // ---- 窗户：天气都在玻璃里 ----
            F(g, white, s, 2, 3, 10, 9);
            using (Brush sky = new SolidBrush(Flash(tick) && weather == 3 ? Color.FromArgb(235, 235, 250) : SkyOf(weather, false)))
                F(g, sky, s, 2.5f, 3.5f, 9, 8);
            GraphicsState gs = g.Save();
            g.SetClip(new Rectangle((int)(2.5f * s), (int)(3.5f * s), 9 * s, 8 * s));
            if (weather == 1)
            {
                using (Brush sun = B(255, 214, 90)) F(g, sun, s, 8.5f, 4.3f, 2, 2);
                float cx = 2 + ((tick / 12f) % 13);
                F(g, Brushes.White, s, cx, 6.5f, 2.5f, 0.8f); F(g, Brushes.White, s, cx + 0.6f, 5.9f, 1.3f, 0.6f);
            }
            else
            {
                using (Brush cloud = B(weather == 3 ? 60 : 168, weather == 3 ? 62 : 176, weather == 3 ? 80 : 190))
                { F(g, cloud, s, 2.5f, 3.5f, 9, 1.2f); F(g, cloud, s, 4, 4.7f, 5, 0.8f); }
            }
            if (weather == 3 && BoltNow(tick)) Bolt(g, s, 8, 4.6f);
            Weather(g, s, weather, tick, 2.5f, 3.5f, 9, 8, 14);
            g.Restore(gs);
            F(g, white, s, 6.75f, 3.5f, 0.5f, 8); F(g, white, s, 2.5f, 7.25f, 9, 0.5f);
            F(g, white, s, 1.5f, 12, 11, 0.7f);

            if ((D & 1) != 0)   // 窗帘
                using (Brush cur = B(225, 120, 120)) using (Brush curD = B(195, 95, 100))
                {
                    F(g, brown, s, 1, 2.4f, 12, 0.4f);
                    F(g, cur, s, 1.1f, 2.8f, 2, 10); F(g, curD, s, 1.8f, 2.8f, 0.3f, 10);
                    F(g, cur, s, 10.9f, 2.8f, 2, 10); F(g, curD, s, 11.6f, 2.8f, 0.3f, 10);
                }
            if ((D & 2) != 0)   // 挂画：小山和太阳
            {
                F(g, brown, s, 16.5f, 3, 6, 5);
                using (Brush c = B(190, 225, 245)) F(g, c, s, 17, 3.5f, 5, 4);
                using (Brush c = B(110, 175, 110)) { F(g, c, s, 17, 6, 5, 1.5f); F(g, c, s, 18, 5.2f, 2, 0.8f); }
                using (Brush c = B(255, 200, 80)) F(g, c, s, 20.6f, 4, 0.8f, 0.8f);
            }
            if ((D & 4) != 0)   // 书架
            {
                F(g, brown, s, 28, 8, 9, 0.5f);
                Color[] bk = { Color.FromArgb(220, 90, 90), Color.FromArgb(90, 140, 220), Color.FromArgb(240, 190, 70), Color.FromArgb(110, 180, 120), Color.FromArgb(170, 120, 210) };
                float bx = 28.4f;
                for (int i = 0; i < 7; i++)
                {
                    float h = 2 + (i * 3 % 4) * 0.4f;
                    using (Brush c = new SolidBrush(bk[i % bk.Length])) F(g, c, s, bx, 8 - h, 0.9f, h);
                    bx += 1.1f;
                }
                using (Brush c = B(120, 180, 110)) { F(g, c, s, 36, 6.3f, 0.8f, 1.7f); F(g, c, s, 35.6f, 5.8f, 0.6f, 0.8f); }
            }
            if ((D & 8) != 0)   // 落地灯
            {
                using (Brush c = B(120, 120, 130)) { F(g, c, s, 36.4f, 11, 0.4f, 9); F(g, c, s, 35.4f, 19.6f, 2.4f, 0.4f); }
                using (Brush c = L.LampOn ? B(250, 220, 150) : B(200, 196, 184)) F(g, c, s, 35, 8.5f, 3.2f, 2.5f);
                if (L.LampOn) using (Brush c = B(255, 244, 200)) F(g, c, s, 35.4f, 11, 2.4f, 0.4f);
            }
            if ((D & 16) != 0)   // 地毯
                using (Brush c = B(205, 125, 150)) using (Brush c2 = B(240, 180, 195))
                { F(g, c, s, 3, 21.4f, 34, 2.2f); F(g, c2, s, 3.5f, 22.3f, 33, 0.4f); }
            if ((D & 32) != 0)   // 挂钟：指针会走
            {
                F(g, brown, s, 24.3f, 2.8f, 2.4f, 2.4f);
                F(g, white, s, 24.6f, 3.1f, 1.8f, 1.8f);
                using (Brush c = B(60, 60, 70))
                {
                    F(g, c, s, 25.4f, 3.4f, 0.2f, 0.7f);
                    int k = (tick / 30) % 4;
                    float[] dx = { 0.2f, 0, -0.6f, 0 }, dy = { 0, 0.2f, 0, -0.6f };
                    F(g, c, s, 25.4f + dx[k], 4f + dy[k], k % 2 == 0 ? 0.6f : 0.2f, k % 2 == 0 ? 0.2f : 0.6f);
                }
            }
            if ((D & 64) != 0)   // 大盆栽
            {
                using (Brush c = B(80, 160, 90)) using (Brush c2 = B(60, 130, 75))
                {
                    F(g, c, s, 1.2f, 13, 1.2f, 4); F(g, c2, s, 2.6f, 12, 1.2f, 5); F(g, c, s, 3.8f, 14, 1.2f, 3);
                    F(g, c2, s, 0.6f, 15, 1, 2);
                }
                using (Brush c = B(200, 110, 75)) F(g, c, s, 1, 17, 4, 3);
                using (Brush c = B(170, 90, 60)) F(g, c, s, 1, 17, 4, 0.5f);
            }
            if ((D & 128) != 0)   // 小彩灯：一闪一闪
            {
                using (Brush wire = B(90, 90, 100)) F(g, wire, s, 0, 1.2f, 40, 0.15f);
                Color[] lc = { Color.FromArgb(255, 110, 110), Color.FromArgb(255, 210, 90), Color.FromArgb(120, 210, 130), Color.FromArgb(110, 170, 255) };
                for (int i = 0; i < 20; i++)
                {
                    bool on = ((tick / 15) + i) % 3 != 0;
                    using (Brush c = new SolidBrush(on ? lc[i % 4] : NestForm.Mix(lc[i % 4], Color.Black, 0.4f)))
                        F(g, c, s, i * 2 + 0.7f, 1.35f, 0.6f, 0.7f);
                }
            }
        }
    }

    static void DrawOutdoor(Graphics g, int s, NestLook L, int weather, int tick, bool night)
    {
        int D = L.OutDecor;
        bool flash = weather == 3 && Flash(tick);
        Color skyC = flash ? Color.FromArgb(220, 222, 240) : SkyOf(weather, night);
        using (Brush sky = new SolidBrush(skyC))
        using (Brush sky2 = new SolidBrush(NestForm.Mix(skyC, Color.White, night ? 0.08f : 0.18f)))
        using (Brush hill = night ? B(45, 85, 65) : B(120, 190, 120))
        using (Brush grass = night ? B(55, 105, 70) : B(140, 205, 110))
        using (Brush grassD = night ? B(40, 85, 55) : B(110, 180, 90))
        using (Brush stemB = night ? B(50, 110, 60) : B(70, 150, 70))
        {
            F(g, sky, s, 0, 0, 40, 16);
            F(g, sky2, s, 0, 8, 40, 8);
            if (night)
            {
                for (int i = 0; i < 24 && weather < 2; i++)   // 阴天下雨看不到星星
                {
                    bool tw = (tick / 20 + i) % 5 != 0;
                    using (Brush st = B(255, 245, tw ? 200 : 130))
                        F(g, st, s, (i * 53 % 400) / 10f, (i * 29 % 110) / 10f, tw ? 0.3f : 0.2f, tw ? 0.3f : 0.2f);
                }
                string[] moon = { "..XXX.", ".XX...", "XX....", "XX....", ".XX...", "..XXX." };   // 月牙
                using (Brush m = B(250, 235, 170))
                    for (int r = 0; r < 6; r++)
                        for (int c = 0; c < 6; c++)
                            if (moon[r][c] == 'X') F(g, m, s, 32 + c * 0.45f, 1.6f + r * 0.45f, 0.45f, 0.45f);
            }
            else if (weather == 1)
            {
                using (Brush sun = B(255, 214, 90)) F(g, sun, s, 32, 2, 3, 3);
                using (Brush sun = B(255, 235, 150)) { F(g, sun, s, 33.2f, 0.8f, 0.6f, 0.8f); F(g, sun, s, 35.4f, 3.2f, 0.8f, 0.6f); F(g, sun, s, 30.8f, 3.2f, 0.8f, 0.6f); }
            }
            if (!night || weather >= 2)   // 云
                for (int k = 0; k < 3; k++)
                {
                    float cx = ((tick / 14f + k * 16) % 52) - 8;
                    int cv = weather == 1 ? 255 : (weather == 3 ? 90 : 185);
                    if (night) cv = 60;
                    using (Brush c = B(cv, cv, Math.Min(255, cv + 10)))
                    { F(g, c, s, cx, 3 + k * 2, 4, 1); F(g, c, s, cx + 1, 2.3f + k * 2, 2, 0.8f); }
                }
            if (weather == 3 && BoltNow(tick)) Bolt(g, s, 25, 2);

            // 远处的小山
            F(g, hill, s, 0, 13, 12, 3); F(g, hill, s, 2, 12, 7, 1);
            F(g, hill, s, 25, 12.5f, 15, 3.5f); F(g, hill, s, 29, 11.5f, 8, 1);

            if ((D & 64) != 0)   // 小树
            {
                using (Brush c = B(120, 85, 55)) F(g, c, s, 3, 10, 1.6f, 6);
                using (Brush c = night ? B(40, 95, 60) : B(80, 160, 85)) { F(g, c, s, 0.6f, 5, 6.4f, 5.5f); F(g, c, s, 1.6f, 4, 4.4f, 1); }
                using (Brush c = night ? B(55, 115, 70) : B(110, 190, 105)) F(g, c, s, 1.4f, 5.6f, 2.4f, 2);
                if (!night) using (Brush c = B(230, 80, 80)) { F(g, c, s, 4.6f, 7, 0.6f, 0.6f); F(g, c, s, 2.2f, 8.6f, 0.6f, 0.6f); }
            }

            F(g, grass, s, 0, 15.5f, 40, 8.5f);
            for (int i = 0; i < 14; i++) F(g, grassD, s, (i * 31 % 400) / 10f, 16.5f + (i * 7 % 70) / 10f, 0.8f, 0.3f);
            if (weather == 4) using (Brush sn = B(240, 244, 250)) { F(g, sn, s, 0, 15.5f, 40, 0.6f); }

            if ((D & 32) != 0)   // 木栅栏
                using (Brush c = B(215, 175, 125))
                {
                    F(g, c, s, 0, 16.6f, 8.5f, 0.4f); F(g, c, s, 0, 18, 8.5f, 0.4f);
                    F(g, c, s, 31.5f, 16.6f, 8.5f, 0.4f); F(g, c, s, 31.5f, 18, 8.5f, 0.4f);
                    for (int x = 0; x < 3; x++) { F(g, c, s, 0.5f + x * 3, 16, 0.6f, 3.2f); F(g, c, s, 32 + x * 3, 16, 0.6f, 3.2f); }
                }
            if ((D & 128) != 0)   // 草丛
                using (Brush c = night ? B(35, 80, 50) : B(90, 165, 85))
                {
                    F(g, c, s, 6.5f, 17.5f, 3, 1.5f); F(g, c, s, 7, 17, 2, 0.5f);
                    F(g, c, s, 30.5f, 18, 3.4f, 1.5f); F(g, c, s, 31, 17.5f, 2.4f, 0.5f);
                }
            if ((D & 4) != 0)   // 向日葵
                foreach (float x in new[] { 5.5f, 35 })
                {
                    F(g, stemB, s, x + 0.6f, 15.5f, 0.3f, 5);
                    using (Brush c = B(250, 205, 50)) F(g, c, s, x, 14, 1.6f, 1.6f);
                    using (Brush c = B(130, 90, 50)) F(g, c, s, x + 0.5f, 14.5f, 0.6f, 0.6f);
                }
            if ((D & 1) != 0)   // 郁金香
            {
                Color[] tc = { Color.FromArgb(235, 80, 90), Color.FromArgb(250, 150, 190), Color.FromArgb(250, 200, 70), Color.FromArgb(180, 120, 220) };
                float[] tx = { 2, 3.6f, 34.4f, 36.4f, 38 };
                for (int i = 0; i < tx.Length; i++)
                {
                    F(g, stemB, s, tx[i] + 0.3f, 20, 0.25f, 1.6f);
                    using (Brush c = new SolidBrush(tc[i % 4])) { F(g, c, s, tx[i], 19.2f, 0.85f, 0.8f); F(g, c, s, tx[i] - 0.1f, 18.9f, 0.25f, 0.4f); F(g, c, s, tx[i] + 0.7f, 18.9f, 0.25f, 0.4f); }
                }
            }
            if ((D & 2) != 0)   // 小雏菊
                foreach (float[] p in new[] { new[] { 1f, 22 }, new[] { 6, 21.5f }, new[] { 33, 22.4f }, new[] { 38.4f, 21.2f }, new[] { 4.2f, 23 } })
                {
                    F(g, Brushes.White, s, p[0] - 0.3f, p[1], 0.9f, 0.3f); F(g, Brushes.White, s, p[0], p[1] - 0.3f, 0.3f, 0.9f);
                    using (Brush c = B(250, 210, 60)) F(g, c, s, p[0], p[1], 0.3f, 0.3f);
                }
            if ((D & 8) != 0)   // 蘑菇
                foreach (float x in new[] { 1.4f, 37 })
                {
                    F(g, Brushes.White, s, x + 0.4f, 21.6f, 0.6f, 0.9f);
                    using (Brush c = B(225, 70, 70)) F(g, c, s, x, 20.8f, 1.4f, 0.8f);
                    F(g, Brushes.White, s, x + 0.3f, 21, 0.25f, 0.25f); F(g, Brushes.White, s, x + 0.9f, 20.9f, 0.25f, 0.25f);
                }
            if ((D & 16) != 0 && weather != 2 && weather != 3)   // 蝴蝶（下雨就躲起来了）
            {
                Color[] bc = { Color.FromArgb(250, 160, 200), Color.FromArgb(150, 200, 255) };
                for (int k = 0; k < 2; k++)
                {
                    float bx = (k == 0 ? 6 : 33) + (float)Math.Sin((tick + k * 50) / 22.0) * 3;
                    float by = 11 + (float)Math.Sin((tick + k * 30) / 13.0) * 1.5f;
                    bool up = (tick / 4 + k) % 2 == 0;
                    using (Brush c = new SolidBrush(bc[k]))
                    {
                        F(g, c, s, bx - 0.5f, by - (up ? 0.4f : 0), 0.5f, up ? 0.7f : 0.4f);
                        F(g, c, s, bx + 0.2f, by - (up ? 0.4f : 0), 0.5f, up ? 0.7f : 0.4f);
                    }
                    using (Brush c = B(60, 50, 50)) F(g, c, s, bx, by - 0.2f, 0.2f, 0.6f);
                }
            }
            if ((D & 256) != 0)   // 萤火虫（晚上更亮）
                for (int i = 0; i < 7; i++)
                {
                    if ((tick / 9 + i * 3) % 5 == 0) continue;
                    float fx = (i * 57 % 400) / 10f + (float)Math.Sin((tick + i * 40) / 30.0) * 1.2f;
                    float fy = 9 + (i * 23 % 90) / 10f + (float)Math.Cos((tick + i * 25) / 25.0);
                    using (Brush c = night ? B(240, 255, 120) : B(200, 220, 90)) F(g, c, s, fx, fy, 0.35f, 0.35f);
                }
            Weather(g, s, weather, tick, 0, 0, 40, 24, 36);
        }
    }
}

class MealForm : Form
{
    const int MAX = 3;
    readonly PetForm pet;
    readonly float K;
    readonly bool en;
    readonly List<int> picks = new List<int>();
    readonly PictureBox tray = new PictureBox();
    readonly Label hint = new Label();
    readonly Button btnGo = new Button(), btnClear = new Button();
    readonly ToolTip tip = new ToolTip();
    static readonly Color Accent = Color.FromArgb(217, 119, 87);

    int S(int v) { return (int)Math.Round(v * K); }
    string T(string zh, string e) { return en ? e : zh; }

    public MealForm(PetForm pet, float k, bool en)
    {
        this.pet = pet; K = k; this.en = en;
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Microsoft YaHei UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = Color.White;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Text = T("套餐", "Meal set");
        ClientSize = new Size(S(424), S(396));

        Label top = new Label();
        top.Text = T("点食物放到餐盘上（最多 3 样），点餐盘上的食物可以拿掉。", "Click foods to put them on the tray (up to 3). Click a food on the tray to take it off.");
        top.SetBounds(S(16), S(12), S(392), S(36));
        Controls.Add(top);

        int cell = S(48);
        for (int i = 0; i < PetForm.Foods.Length; i++)
        {
            int idx = i;
            Button b = new Button();
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(215, 215, 222);
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 236, 226);
            b.BackColor = Color.White;
            b.Image = PetForm.ArtIcon(PetForm.Foods[i], Math.Max(3, S(4)));
            b.SetBounds(S(16) + (i % 8) * (cell + S(1)), S(52) + (i / 8) * (cell + S(1)), cell, cell);
            tip.SetToolTip(b, en ? PetForm.FoodEn[i] : PetForm.FoodZh[i]);
            b.Click += delegate { Add(idx); };
            Controls.Add(b);
        }

        int gy = S(52) + ((PetForm.Foods.Length + 7) / 8) * (cell + S(1)) + S(8);
        tray.SetBounds(S(16), gy, S(392), S(110));
        tray.BackColor = Color.FromArgb(236, 226, 212);
        tray.Paint += PaintTray;
        tray.MouseClick += delegate (object s, MouseEventArgs e)
        {
            int slot = e.X * MAX / tray.Width;
            if (slot < picks.Count) { picks.RemoveAt(slot); Changed(); }
        };
        Controls.Add(tray);

        hint.SetBounds(S(16), gy + S(118), S(392), S(20));
        hint.ForeColor = Color.Gray;
        Controls.Add(hint);

        btnClear.Text = T("清空", "Clear");
        btnClear.SetBounds(S(232), gy + S(144), S(80), S(30));
        btnClear.Click += delegate { picks.Clear(); Changed(); };
        Controls.Add(btnClear);

        btnGo.Text = T("开饭!", "Serve!");
        btnGo.SetBounds(S(328), gy + S(144), S(80), S(30));
        ClientSize = new Size(S(424), gy + S(186));
        btnGo.BackColor = Accent; btnGo.ForeColor = Color.White;
        btnGo.FlatStyle = FlatStyle.Flat; btnGo.FlatAppearance.BorderSize = 0;
        btnGo.Click += delegate
        {
            if (picks.Count == 0) { hint.Text = T("先放点吃的到餐盘上~", "Put some food on the tray first~"); return; }
            pet.ServeMeal(picks.ToArray());
            Close();
        };
        Controls.Add(btnGo);
        Changed();
    }

    // 自检：把套餐窗口画成图
    internal static void RenderSelf(string path)
    {
        PetForm p = new PetForm(true);
        MealForm m = new MealForm(p, p.K, false);
        m.Add(2); m.Add(5); m.Add(12);
        m.StartPosition = FormStartPosition.Manual; m.Location = new Point(-4000, -4000);
        m.Show(); Application.DoEvents();
        using (Bitmap bmp = new Bitmap(m.Width, m.Height))
        {
            m.DrawToBitmap(bmp, new Rectangle(0, 0, m.Width, m.Height));
            bmp.Save(path, ImageFormat.Png);
        }
    }

    void Add(int idx)
    {
        if (picks.Count >= MAX) { hint.Text = T("餐盘满啦，最多 3 样~", "The tray is full (3 max)~"); return; }
        picks.Add(idx); Changed();
    }

    void Changed()
    {
        tray.Invalidate();
        hint.Text = picks.Count < MAX ? T("餐盘上还能放 ", "Room for ") + (MAX - picks.Count) + T(" 样", " more")
            : T("餐盘满啦，点「开饭!」端给 Claude~", "Tray is full. Click Serve!~");
        btnGo.Enabled = picks.Count > 0;
    }

    void PaintTray(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        int w = tray.Width, h = tray.Height;
        int ty = h - S(26);
        using (Brush wood = new SolidBrush(Color.FromArgb(170, 120, 80)), rim = new SolidBrush(Color.FromArgb(130, 88, 58)))
        {
            g.FillRectangle(wood, S(12), ty, w - S(24), S(12));
            g.FillRectangle(rim, S(8), ty - S(6), S(6), S(18));
            g.FillRectangle(rim, w - S(14), ty - S(6), S(6), S(18));
        }
        int fp = Math.Max(3, S(7)), slotW = w / MAX;
        for (int i = 0; i < picks.Count; i++)
            PetForm.DrawArt(g, PetForm.Foods[picks[i]], i * slotW + (slotW - 8 * fp) / 2, ty - 8 * fp, fp, 0);
        if (picks.Count == 0)
            using (Brush tb = new SolidBrush(Color.FromArgb(170, 160, 150)))
                g.DrawString(T("（空餐盘）", "(empty tray)"), Font, tb, w / 2 - S(36), S(30));
    }
}

class SettingsForm : Form
{
    readonly PetForm pet, pv;
    readonly float K;
    readonly GroupBox gLang = new GroupBox();
    readonly RadioButton rZh = new RadioButton(), rEn = new RadioButton();
    readonly TabControl tabs = new TabControl();
    readonly TabPage tDress = new TabPage(), tNest = new TabPage();
    readonly Button btnClose = new Button();
    // 装扮页
    readonly PictureBox preview = new PictureBox();
    readonly Label previewCaption = new Label(), dressHint = new Label();
    readonly Button btnRand = new Button(), btnClear = new Button();
    readonly Panel dressPanel = new Panel();
    readonly Label[] slotLabels = new Label[PetForm.SLOTS];
    readonly List<Button>[] slotBtns = new List<Button>[PetForm.SLOTS];
    // 小窝页
    readonly PictureBox nestPreview = new PictureBox();
    readonly Label nestCaption = new Label();
    readonly Panel nestPanel = new Panel();
    readonly CheckBox chkNest = new CheckBox();
    readonly Label lblType = new Label(), lblColor = new Label(), lblPattern = new Label(), lblHint = new Label();
    readonly List<Button> typeBtns = new List<Button>(), colorBtns = new List<Button>(), patternBtns = new List<Button>();
    readonly Button btnBed = new Button(), btnHang = new Button();
    readonly Label lblDecor = new Label(), lblFlag = new Label(), lblFlagNote = new Label();
    readonly List<CheckBox> decorChks = new List<CheckBox>();
    readonly List<Button> flagBtns = new List<Button>();
    readonly Label lblPlushL = new Label(), lblPlushR = new Label(), lblPlushNote = new Label();
    readonly List<Button> plushLBtns = new List<Button>(), plushRBtns = new List<Button>();
    // 可以收起的两段：小窝 / 背景
    readonly Button hdrNest = new Button(), hdrBg = new Button(), hdrLight = new Button();
    readonly Panel secNest = new Panel(), secBg = new Panel(), secLight = new Panel();
    readonly Label scrollEnd = new Label();
    bool nestFolded, bgFolded, lightFolded;
    readonly Label lblScene = new Label(), lblWall = new Label(), lblRoom = new Label(), lblOut = new Label(), lblWeather = new Label(), lblBgNote = new Label();
    readonly List<Button> sceneBtns = new List<Button>(), wallBtns = new List<Button>(), weatherBtns = new List<Button>();
    readonly List<CheckBox> roomChks = new List<CheckBox>(), outChks = new List<CheckBox>();
    readonly Label lblLight = new Label();
    readonly List<Button> lightBtns = new List<Button>();
    CheckBox chkLamp;
    readonly Label lblGlow = new Label(), lblGlowNote = new Label(), lblGlowColor = new Label();
    readonly Label[] glowNames = { new Label(), new Label(), new Label() };
    readonly TrackBar[] glowBars = { new TrackBar(), new TrackBar(), new TrackBar() };
    readonly List<Button> glowBtns = new List<Button>();
    Bitmap glowPrev; string glowPrevKey;
    CheckBox chkGlow;
    readonly Label lblBgAlpha = new Label();
    readonly TrackBar bgAlphaBar = new TrackBar();

    readonly ToolTip tip = new ToolTip();
    readonly Timer anim = new Timer();
    readonly Random rand = new Random();
    readonly Font zf = new Font("Consolas", 11f, FontStyle.Bold);
    Bitmap snap;
    int blinkT, zt;
    bool syncing;
    static readonly Color Accent = Color.FromArgb(217, 119, 87);
    static readonly Color AccentSoft = Color.FromArgb(255, 236, 226);
    static readonly Color Line = Color.FromArgb(215, 215, 222);

    int S(int v) { return (int)Math.Round(v * K); }

    public SettingsForm(PetForm pet, PetForm pv, float k)
    {
        this.pet = pet; this.pv = pv; K = k;
        for (int i = 0; i < slotBtns.Length; i++) slotBtns[i] = new List<Button>();
        AutoScaleMode = AutoScaleMode.None;
        Font = new Font("Microsoft YaHei UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = Color.White;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        ClientSize = new Size(S(680), S(552));

        gLang.SetBounds(S(16), S(8), S(648), S(56));
        gLang.Text = "语言 / Language";
        rZh.Text = "中文"; rZh.AutoSize = true; rZh.Location = new Point(S(16), S(24));
        rEn.Text = "English"; rEn.AutoSize = true; rEn.Location = new Point(S(120), S(24));
        rZh.CheckedChanged += LangChanged; rEn.CheckedChanged += LangChanged;
        gLang.Controls.Add(rZh); gLang.Controls.Add(rEn);
        Controls.Add(gLang);

        tabs.SetBounds(S(16), S(74), S(648), S(424));
        foreach (TabPage tp in new[] { tDress, tNest })
        {
            tp.UseVisualStyleBackColor = false; tp.BackColor = Color.White;
            tabs.TabPages.Add(tp);
        }
        Controls.Add(tabs);

        BuildDressPage();
        BuildNestPage();

        Btn(this, btnClose, 584, 510, 80, 30);
        btnClose.BackColor = Accent; btnClose.ForeColor = Color.White;
        btnClose.Click += delegate { Close(); };

        anim.Interval = 120;
        anim.Tick += delegate
        {
            if (blinkT > 0) { blinkT--; if (blinkT == 0) UpdateSnap(); }
            else if (rand.Next(25) == 0) { blinkT = 2; UpdateSnap(); }
            if (++zt % 5 == 0 || pet.Look.Bg > 0) nestPreview.Invalidate();
        };
        anim.Start();
        FormClosed += delegate { anim.Stop(); };
        RefreshAll();
    }

    internal void SelectTab(int i) { tabs.SelectedIndex = i; }
    internal void ScrollTo(int tab, int y) { (tab == 0 ? dressPanel : nestPanel).AutoScrollPosition = new Point(0, S(y)); }

    Label Lbl(Control parent, Label l, int x, int y)
    {
        l.AutoSize = true; l.Location = new Point(S(x), S(y)); parent.Controls.Add(l); return l;
    }
    static void Spacer(Control parent, int x, int y)
    {
        Label sp = new Label(); sp.Location = new Point(x, y); sp.Size = new Size(1, 1); parent.Controls.Add(sp);
    }
    Panel ScrollPanel(TabPage tp, Panel p)
    {
        p.SetBounds(S(218), S(4), S(414), S(386));
        p.AutoScroll = true;
        p.BorderStyle = BorderStyle.FixedSingle;
        p.BackColor = Color.White;
        tp.Controls.Add(p);
        return p;
    }

    void BuildDressPage()
    {
        preview.SetBounds(S(8), S(8), S(200), S(200));
        preview.BackColor = Color.FromArgb(252, 246, 240);
        preview.Paint += PaintPreview;
        tDress.Controls.Add(preview);

        previewCaption.SetBounds(S(8), S(212), S(200), S(20));
        previewCaption.TextAlign = ContentAlignment.MiddleCenter;
        previewCaption.ForeColor = Color.FromArgb(120, 120, 130);
        tDress.Controls.Add(previewCaption);

        Btn(tDress, btnRand, 8, 238, 200, 32);
        btnRand.Click += delegate { pet.RandomOutfit(); };
        Btn(tDress, btnClear, 8, 276, 200, 32);
        btnClear.Click += delegate { pet.ClearOutfit(); };
        dressHint.AutoSize = false;
        dressHint.SetBounds(S(8), S(318), S(205), S(60));
        dressHint.ForeColor = Color.FromArgb(130, 130, 140);
        tDress.Controls.Add(dressHint);

        ScrollPanel(tDress, dressPanel);
        int isz = Math.Max(2, 2 * (int)Math.Round(2 * K));
        int y = 6;
        for (int s = 0; s < PetForm.SLOTS; s++)
        {
            Label l = new Label();
            l.AutoSize = false;
            l.SetBounds(S(8), S(y), S(370), S(20));
            slotLabels[s] = l; dressPanel.Controls.Add(l);
            y += 24;
            int n = PetForm.OutfitCount(s);
            for (int i = 0; i < n; i++)
            {
                int slot = s, idx = i;
                int[] fit = new int[PetForm.SLOTS]; fit[s] = i;
                Button b = new Button();
                b.SetBounds(S(8 + (i % 7) * 55), S(y + (i / 7) * 55), S(52), S(52));
                b.FlatStyle = FlatStyle.Flat;
                b.Image = pv.Snapshot(isz, fit, false);
                b.ImageAlign = ContentAlignment.MiddleCenter;
                b.Cursor = Cursors.Hand;
                b.Click += delegate { pet.SetOutfit(slot, idx); };
                slotBtns[s].Add(b); dressPanel.Controls.Add(b);
            }
            y += ((n + 6) / 7) * 55 + 10;
        }
        Spacer(dressPanel, 0, S(y));
    }

    Button Swatch(Control parent, Color c, int x, int y)
    {
        Button b = new Button();
        b.SetBounds(S(x), S(y), S(40), S(40));
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = c;
        b.Cursor = Cursors.Hand;
        parent.Controls.Add(b);
        return b;
    }

    void BuildNestPage()
    {
        nestPreview.SetBounds(S(8), S(8), S(200), S(200));
        nestPreview.BackColor = Color.FromArgb(252, 246, 240);
        nestPreview.Paint += PaintNest;
        tNest.Controls.Add(nestPreview);
        nestCaption.SetBounds(S(8), S(212), S(200), S(20));
        nestCaption.TextAlign = ContentAlignment.MiddleCenter;
        nestCaption.ForeColor = Color.FromArgb(120, 120, 130);
        tNest.Controls.Add(nestCaption);
        Btn(tNest, btnBed, 8, 238, 200, 32);
        btnBed.Click += delegate { pet.GoSleepNow(); };
        Btn(tNest, btnHang, 8, 276, 200, 32);
        btnHang.Click += delegate { pet.GoNestNow(); };
        lblHint.AutoSize = false;
        lblHint.SetBounds(S(8), S(316), S(205), S(80));
        lblHint.ForeColor = Color.FromArgb(110, 110, 120);
        tNest.Controls.Add(lblHint);

        ScrollPanel(tNest, nestPanel);
        Header(hdrNest, delegate { nestFolded = !nestFolded; LayoutNestPanel(); });
        Header(hdrBg, delegate { bgFolded = !bgFolded; LayoutNestPanel(); });
        Header(hdrLight, delegate { lightFolded = !lightFolded; LayoutNestPanel(); });
        secLight.SetBounds(0, 0, S(392), S(330)); secLight.BackColor = Color.White; nestPanel.Controls.Add(secLight);
        secNest.SetBounds(0, 0, S(392), S(764)); secNest.BackColor = Color.White; nestPanel.Controls.Add(secNest);
        secBg.SetBounds(0, 0, S(392), S(556)); secBg.BackColor = Color.White; nestPanel.Controls.Add(secBg);
        scrollEnd.Size = new Size(1, 1); nestPanel.Controls.Add(scrollEnd);
        Panel P = secNest;
        BuildBgSection(secBg);

        chkNest.AutoSize = true;
        chkNest.Location = new Point(S(8), S(6));
        chkNest.Font = new Font(Font, FontStyle.Bold);
        chkNest.CheckedChanged += delegate { if (!syncing) pet.SetNestEnabled(chkNest.Checked); };
        P.Controls.Add(chkNest);

        // 种类
        Lbl(P, lblType, 8, 36);
        for (int i = 0; i < NestForm.TypeZh.Length; i++)
        {
            int idx = i;
            Button b = new Button();
            b.SetBounds(S(8 + (i % 3) * 126), S(58 + (i / 3) * 78), S(120), S(72));
            b.FlatStyle = FlatStyle.Flat;
            b.TextImageRelation = TextImageRelation.ImageAboveText;
            b.Cursor = Cursors.Hand;
            b.Click += delegate { pet.Look.Type = idx; pet.NestLookChanged(); };
            typeBtns.Add(b); P.Controls.Add(b);
        }

        // 颜色
        Lbl(P, lblColor, 8, 216);
        for (int i = 0; i < NestForm.Colors.Length; i++)
        {
            int idx = i;
            Button b = Swatch(P, NestForm.Colors[i], 8 + i * 47, 238);
            b.Click += delegate { pet.Look.Color = idx; pet.NestLookChanged(); };
            colorBtns.Add(b);
        }

        // 花纹
        Lbl(P, lblPattern, 8, 288);
        for (int i = 0; i < NestForm.PatternZh.Length; i++)
        {
            int idx = i;
            Button b = new Button();
            b.SetBounds(S(8 + i * 75), S(310), S(70), S(46));
            b.FlatStyle = FlatStyle.Flat;
            b.TextImageRelation = TextImageRelation.ImageAboveText;
            b.Cursor = Cursors.Hand;
            b.Click += delegate { pet.Look.Pattern = idx; pet.NestLookChanged(); };
            patternBtns.Add(b); P.Controls.Add(b);
        }

        // 装饰
        Lbl(P, lblDecor, 8, 368);
        for (int i = 0; i < NestForm.DecorZh.Length; i++)
        {
            int bit = 1 << i;
            CheckBox c = new CheckBox();
            c.Appearance = Appearance.Button;
            c.FlatStyle = FlatStyle.Flat;
            c.TextAlign = ContentAlignment.MiddleCenter;
            c.SetBounds(S(8 + (i % 5) * 75), S(390 + (i / 5) * 40), S(70), S(34));
            c.Cursor = Cursors.Hand;
            c.FlatAppearance.CheckedBackColor = AccentSoft;
            c.CheckedChanged += delegate
            {
                if (syncing) return;
                pet.Look.Decor = c.Checked ? (pet.Look.Decor | bit) : (pet.Look.Decor & ~bit);
                pet.NestLookChanged();
            };
            decorChks.Add(c); P.Controls.Add(c);
        }

        // 小旗子颜色
        Lbl(P, lblFlag, 8, 474);
        for (int i = 0; i < NestForm.FlagColors.Length; i++)
        {
            int idx = i;
            Button b = Swatch(P, NestForm.FlagColors[i], 8 + i * 47, 496);
            b.Click += delegate
            {
                pet.Look.FlagColor = idx;
                pet.Look.Decor |= 8;    // 选了颜色就顺便把小旗子插上
                pet.NestLookChanged();
            };
            flagBtns.Add(b);
        }
        lblFlagNote.ForeColor = Color.FromArgb(130, 130, 140);
        Lbl(P, lblFlagNote, 8, 542);

        // 玩偶
        int fp = Math.Max(3, (S(48) - 10) / 8);
        Lbl(P, lblPlushL, 8, 572);
        Lbl(P, lblPlushR, 8, 652);
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < NestForm.PlushZh.Length; i++)
            {
                int idx = i, sd = side;
                Button b = new Button();
                b.SetBounds(S(8 + i * 53), S(594 + side * 80), S(48), S(48));
                b.FlatStyle = FlatStyle.Flat;
                b.Cursor = Cursors.Hand;
                if (i > 0) b.Image = PetForm.ArtIcon(NestForm.Plush[i], fp);
                b.Click += delegate
                {
                    if (sd == 0) pet.Look.PlushL = idx; else pet.Look.PlushR = idx;
                    pet.NestLookChanged();
                };
                (side == 0 ? plushLBtns : plushRBtns).Add(b);
                P.Controls.Add(b);
            }
        lblPlushNote.ForeColor = Color.FromArgb(130, 130, 140);
        Lbl(P, lblPlushNote, 8, 730);

        // 灯光（单独一段）：小窝的主灯光
        P = secLight;
        lblGlow.Font = new Font(Font, FontStyle.Bold);
        Lbl(P, lblGlow, 8, 6);
        lblGlowNote.ForeColor = Color.FromArgb(130, 130, 140);
        Lbl(P, lblGlowNote, 8, 28);
        chkGlow = Toggle(P, 256, 6);
        chkGlow.Width = S(120); chkGlow.Height = S(40);
        chkGlow.CheckedChanged += delegate { if (syncing) return; pet.Look.GlowOn = chkGlow.Checked; pet.NestLookChanged(); };
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            glowNames[i].AutoSize = false; glowNames[i].SetBounds(S(8), S(52 + i * 40), S(80), S(24));
            glowNames[i].TextAlign = ContentAlignment.MiddleLeft;
            P.Controls.Add(glowNames[i]);
            TrackBar tb = glowBars[i];
            tb.Minimum = 0; tb.Maximum = 100; tb.TickStyle = TickStyle.None; tb.AutoSize = false;
            tb.SetBounds(S(90), S(54 + i * 40), S(280), S(32));
            tb.ValueChanged += delegate
            {
                if (syncing) return;
                int v = glowBars[idx].Value;
                if (idx == 0) pet.Look.GlowAlpha = v; else if (idx == 1) pet.Look.GlowSpread = v; else pet.Look.GlowSize = v;
                pet.GlowChanged(); nestPreview.Invalidate();
            };
            P.Controls.Add(tb);
        }
        Lbl(P, lblGlowColor, 8, 176);
        for (int i = 0; i < LightForm.GlowColors.Length; i++)
        {
            int idx = i;
            Button b = Swatch(P, LightForm.GlowColors[i], 8 + i * 47, 198);
            b.Click += delegate { pet.Look.GlowColor = idx; pet.GlowChanged(); RefreshAll(); };
            glowBtns.Add(b);
        }
        LayoutNestPanel();
    }

    void Header(Button h, EventHandler click)
    {
        h.FlatStyle = FlatStyle.Flat;
        h.FlatAppearance.BorderColor = Line;
        h.BackColor = Color.FromArgb(248, 244, 240);
        h.TextAlign = ContentAlignment.MiddleLeft;
        h.Font = new Font(Font, FontStyle.Bold);
        h.Cursor = Cursors.Hand;
        h.Size = new Size(S(384), S(30));
        h.Click += click;
        nestPanel.Controls.Add(h);
    }

    // 摆放两段内容；收起的那段只剩标题
    void LayoutNestPanel()
    {
        int y = nestPanel.AutoScrollPosition.Y + S(4);
        hdrNest.Location = new Point(S(4), y); y += S(34);
        secNest.Visible = !nestFolded;
        if (!nestFolded) { secNest.Location = new Point(0, y); y += secNest.Height; }
        hdrLight.Location = new Point(S(4), y); y += S(34);
        secLight.Visible = !lightFolded;
        if (!lightFolded) { secLight.Location = new Point(0, y); y += secLight.Height; }
        hdrBg.Location = new Point(S(4), y); y += S(34);
        secBg.Visible = !bgFolded;
        if (!bgFolded) { secBg.Location = new Point(0, y); y += secBg.Height; }
        scrollEnd.Location = new Point(0, y + S(6));
        hdrNest.Text = (nestFolded ? "▶  " : "▼  ") + T("小窝", "Bed");
        hdrBg.Text = (bgFolded ? "▶  " : "▼  ") + T("背景", "Background");
        hdrLight.Text = (lightFolded ? "▶  " : "▼  ") + T("灯光", "Lighting");
    }

    CheckBox Toggle(Control parent, int x, int y)
    {
        CheckBox c = new CheckBox();
        c.Appearance = Appearance.Button;
        c.FlatStyle = FlatStyle.Flat;
        c.TextAlign = ContentAlignment.MiddleCenter;
        c.SetBounds(S(x), S(y), S(70), S(34));
        c.Cursor = Cursors.Hand;
        c.FlatAppearance.CheckedBackColor = AccentSoft;
        parent.Controls.Add(c);
        return c;
    }

    void BuildBgSection(Panel P)
    {
        Lbl(P, lblScene, 8, 6);
        for (int i = 0; i < BackdropForm.SceneZh.Length; i++)
        {
            int idx = i;
            Button b = new Button();
            b.SetBounds(S(8 + i * 94), S(28), S(90), S(74));
            b.FlatStyle = FlatStyle.Flat;
            b.TextImageRelation = TextImageRelation.ImageAboveText;
            b.Cursor = Cursors.Hand;
            b.Click += delegate { pet.Look.Bg = idx; pet.NestLookChanged(); };
            sceneBtns.Add(b); P.Controls.Add(b);
        }
        Lbl(P, lblWall, 8, 112);
        for (int i = 0; i < BackdropForm.WallColors.Length; i++)
        {
            int idx = i;
            Button b = Swatch(P, BackdropForm.WallColors[i], 8 + i * 47, 134);
            b.Click += delegate { pet.Look.Wall = idx; if (pet.Look.Bg == 0) pet.Look.Bg = 1; pet.NestLookChanged(); };
            wallBtns.Add(b);
        }
        Lbl(P, lblRoom, 8, 184);
        for (int i = 0; i < BackdropForm.RoomZh.Length; i++)
        {
            int bit = 1 << i;
            CheckBox c = Toggle(P, 8 + (i % 5) * 75, 206 + (i / 5) * 40);
            c.CheckedChanged += delegate
            {
                if (syncing) return;
                pet.Look.RoomDecor = c.Checked ? (pet.Look.RoomDecor | bit) : (pet.Look.RoomDecor & ~bit);
                pet.NestLookChanged();
            };
            roomChks.Add(c);
        }
        Lbl(P, lblOut, 8, 292);
        for (int i = 0; i < BackdropForm.OutZh.Length; i++)
        {
            int bit = 1 << i;
            CheckBox c = Toggle(P, 8 + (i % 5) * 75, 314 + (i / 5) * 40);
            c.CheckedChanged += delegate
            {
                if (syncing) return;
                pet.Look.OutDecor = c.Checked ? (pet.Look.OutDecor | bit) : (pet.Look.OutDecor & ~bit);
                pet.NestLookChanged();
            };
            outChks.Add(c);
        }
        Lbl(P, lblWeather, 8, 400);
        for (int i = 0; i < BackdropForm.WeatherZh.Length; i++)
        {
            int idx = i;
            Button b = new Button();
            b.SetBounds(S(8 + i * 75), S(422), S(70), S(34));
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.Click += delegate { pet.Look.Weather = idx; pet.NestLookChanged(); };
            weatherBtns.Add(b); P.Controls.Add(b);
        }
        Lbl(secLight, lblLight, 8, 250);
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            Button b = new Button();
            b.SetBounds(S(8 + i * 75), S(272), S(70), S(34));
            b.FlatStyle = FlatStyle.Flat;
            b.Cursor = Cursors.Hand;
            b.Click += delegate { pet.Look.Light = idx; pet.NestLookChanged(); };
            lightBtns.Add(b); secLight.Controls.Add(b);
        }
        chkLamp = Toggle(secLight, 8 + 3 * 75, 272);
        chkLamp.Width = S(145);
        chkLamp.CheckedChanged += delegate { if (syncing) return; pet.Look.LampOn = chkLamp.Checked; pet.NestLookChanged(); };
        lblBgNote.ForeColor = Color.FromArgb(130, 130, 140);
        lblBgNote.AutoSize = false;
        lblBgNote.SetBounds(S(8), S(508), S(370), S(44));
        Lbl(P, lblBgAlpha, 8, 466);
        bgAlphaBar.Minimum = 10; bgAlphaBar.Maximum = 100; bgAlphaBar.TickStyle = TickStyle.None; bgAlphaBar.AutoSize = false;
        bgAlphaBar.SetBounds(S(130), S(462), S(240), S(32));
        bgAlphaBar.ValueChanged += delegate
        {
            if (syncing) return;
            pet.Look.BgAlpha = bgAlphaBar.Value; pet.BgAlphaChanged(); nestPreview.Invalidate();
        };
        P.Controls.Add(bgAlphaBar);
        P.Controls.Add(lblBgNote);
    }

    void Btn(Control parent, Button b, int x, int y, int w, int h)
    {
        b.SetBounds(S(x), S(y), S(w), S(h));
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Accent;
        b.BackColor = AccentSoft;
        b.Cursor = Cursors.Hand;
        parent.Controls.Add(b);
    }

    void LangChanged(object sender, EventArgs e)
    {
        if (syncing) return;
        RadioButton r = (RadioButton)sender;
        if (!r.Checked) return;
        pet.SetLang(rEn.Checked);
    }

    string T(string zh, string en) { return pet.En ? en : zh; }

    static void Mark(Button b, bool sel)
    {
        b.BackColor = sel ? AccentSoft : Color.White;
        b.FlatAppearance.BorderColor = sel ? Accent : Line;
        b.FlatAppearance.BorderSize = sel ? 2 : 1;
    }
    static void MarkSwatch(Button b, bool sel)
    {
        b.FlatAppearance.BorderColor = sel ? Color.FromArgb(50, 50, 55) : Line;
        b.FlatAppearance.BorderSize = sel ? 3 : 1;
    }

    internal void RefreshAll()
    {
        bool en = pet.En;
        NestLook L = pet.Look;
        syncing = true;
        rZh.Checked = !en; rEn.Checked = en;
        chkNest.Checked = pet.NestOn;
        for (int i = 0; i < decorChks.Count; i++) decorChks[i].Checked = (L.Decor & (1 << i)) != 0;
        for (int i = 0; i < roomChks.Count; i++) roomChks[i].Checked = (L.RoomDecor & (1 << i)) != 0;
        for (int i = 0; i < outChks.Count; i++) outChks[i].Checked = (L.OutDecor & (1 << i)) != 0;
        if (chkLamp != null) chkLamp.Checked = L.LampOn;
        if (chkGlow != null) chkGlow.Checked = L.GlowOn;
        bgAlphaBar.Value = Math.Max(10, Math.Min(100, L.BgAlpha));
        glowBars[0].Value = L.GlowAlpha; glowBars[1].Value = L.GlowSpread; glowBars[2].Value = L.GlowSize;
        syncing = false;

        Text = T("Claude 桌宠 · 设置", "Claude Pet · Settings");
        tDress.Text = T("装扮", "Dress up");
        tNest.Text = T("小窝", "Bed");
        previewCaption.Text = T("预览", "Preview");
        btnRand.Text = T("随机搭配 ✨", "Randomize ✨");
        btnClear.Text = T("全部脱掉", "Remove all");
        btnClose.Text = T("完成", "Done");
        dressHint.Text = T("右边的衣柜可以用鼠标滚轮上下翻", "Scroll the wardrobe on the right with the mouse wheel");

        int[] fit = pet.Outfit;
        for (int s = 0; s < PetForm.SLOTS; s++)
        {
            slotLabels[s].Text = PetForm.SlotName(s, en) + T("：", ": ") + PetForm.OutfitName(s, fit[s], en);
            for (int i = 0; i < slotBtns[s].Count; i++)
            {
                Mark(slotBtns[s][i], fit[s] == i);
                tip.SetToolTip(slotBtns[s][i], PetForm.OutfitName(s, i, en));
            }
        }

        // 小窝
        nestCaption.Text = pet.NestOn ? T("预览", "Preview") : T("预览（小窝已关闭）", "Preview (bed is off)");
        chkNest.Text = T("显示小窝", "Show the bed");
        lblType.Text = T("种类", "Type");
        lblColor.Text = T("颜色", "Color");
        lblPattern.Text = T("花纹（皮肤）", "Pattern (skin)");
        lblHint.Text = T("· 右边分成「小窝」「灯光」「背景」三段，点标题可以展开 / 收起\n· 小窝可以在桌面上随意拖动\n· 睡着时点一下叫醒，醒着时再点会跳出来",
                         "· Three sections on the right: Bed, Lighting, Background. Click a title to open / fold it\n· Drag the bed anywhere on the desktop\n· Click once to wake it, click again to make it hop out");
        btnBed.Text = T("现在去小窝睡觉 Zz", "Go to bed now Zz");
        btnBed.Enabled = pet.NestOn;
        btnHang.Text = T("去小窝里玩 ♪", "Hang out in the bed ♪");
        btnHang.Enabled = pet.NestOn;
        int tsz = Math.Max(2, (S(120) - 10) / NestForm.NC);
        for (int i = 0; i < typeBtns.Count; i++)
        {
            NestLook li = L.Clone(); li.Type = i; li.Decor = 0; li.PlushL = 0; li.PlushR = 0;
            Bitmap bmp = new Bitmap(NestForm.NC * tsz, NestForm.NR * tsz);
            using (Graphics g = Graphics.FromImage(bmp)) NestForm.DrawNest(g, tsz, li, null, new int[PetForm.SLOTS]);
            Image old = typeBtns[i].Image; typeBtns[i].Image = bmp; if (old != null) old.Dispose();
            typeBtns[i].Text = en ? NestForm.TypeEn[i] : NestForm.TypeZh[i];
            Mark(typeBtns[i], L.Type == i);
        }
        for (int i = 0; i < colorBtns.Count; i++)
        {
            MarkSwatch(colorBtns[i], L.Color == i);
            tip.SetToolTip(colorBtns[i], en ? NestForm.ColorEn[i] : NestForm.ColorZh[i]);
        }
        for (int i = 0; i < patternBtns.Count; i++)
        {
            Bitmap bmp = new Bitmap(S(40), S(14));
            using (Graphics g = Graphics.FromImage(bmp))
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddRectangle(new Rectangle(0, 0, bmp.Width, bmp.Height));
                NestForm.FillSkin(g, Math.Max(4, S(6)), NestForm.Colors[L.Color], i, p);
            }
            Image old = patternBtns[i].Image; patternBtns[i].Image = bmp; if (old != null) old.Dispose();
            patternBtns[i].Text = en ? NestForm.PatternEn[i] : NestForm.PatternZh[i];
            Mark(patternBtns[i], L.Pattern == i);
        }

        // 装饰
        lblDecor.Text = T("装饰（可以多选）", "Decorations (pick any)");
        lblFlag.Text = T("小旗子的颜色", "Flag color");
        lblFlagNote.Text = T("选颜色会顺便把小旗子插上", "Picking a color also puts the flag up");
        for (int i = 0; i < decorChks.Count; i++)
        {
            decorChks[i].Text = en ? NestForm.DecorEn[i] : NestForm.DecorZh[i];
            decorChks[i].FlatAppearance.BorderColor = decorChks[i].Checked ? Accent : Line;
        }
        for (int i = 0; i < flagBtns.Count; i++)
        {
            MarkSwatch(flagBtns[i], L.FlagColor == i);
            tip.SetToolTip(flagBtns[i], en ? NestForm.FlagEn[i] : NestForm.FlagZh[i]);
        }

        // 玩偶
        lblPlushL.Text = T("左边的玩偶：", "Left plushie: ") + (en ? NestForm.PlushEn[L.PlushL] : NestForm.PlushZh[L.PlushL]);
        lblPlushR.Text = T("右边的玩偶：", "Right plushie: ") + (en ? NestForm.PlushEn[L.PlushR] : NestForm.PlushZh[L.PlushR]);
        lblPlushNote.Text = T("玩偶会陪 Claude 一起在小窝里睡觉", "Plushies keep Claude company in bed");
        for (int i = 0; i < plushLBtns.Count; i++)
        {
            Mark(plushLBtns[i], L.PlushL == i);
            Mark(plushRBtns[i], L.PlushR == i);
            string nm = en ? NestForm.PlushEn[i] : NestForm.PlushZh[i];
            plushLBtns[i].Text = i == 0 ? nm : ""; plushRBtns[i].Text = i == 0 ? nm : "";
            tip.SetToolTip(plushLBtns[i], nm); tip.SetToolTip(plushRBtns[i], nm);
        }

        lblGlow.Text = T("小窝的发光", "Bed glow");
        lblGlowNote.Text = T("只影响小窝自带的那圈光", "Only the bed's own glow");
        chkGlow.Text = L.GlowOn ? T("发光：开", "Glow: on") : T("发光：关", "Glow: off");
        chkGlow.FlatAppearance.BorderColor = L.GlowOn ? Accent : Line;
        lblBgAlpha.Text = T("背景透明度", "Background opacity");
        string[] gz = { "透明度", "扩散范围", "大小" }, ge = { "Opacity", "Softness", "Size" };
        for (int i = 0; i < 3; i++) glowNames[i].Text = en ? ge[i] : gz[i];
        lblGlowColor.Text = T("灯光颜色", "Light color");
        for (int i = 0; i < glowBtns.Count; i++)
        {
            MarkSwatch(glowBtns[i], L.GlowColor == i);
            tip.SetToolTip(glowBtns[i], en ? LightForm.GlowEn[i] : LightForm.GlowZh[i]);
        }

        // 背景
        LayoutNestPanel();
        lblScene.Text = T("场景", "Scene");
        lblWall.Text = T("墙纸颜色（温馨小屋）", "Wallpaper (cozy room)");
        lblRoom.Text = T("房间里的装饰（可以多选）", "Room decorations (pick any)");
        lblOut.Text = T("户外的装饰（草地 / 星空，可以多选）", "Outdoor decorations (meadow / night, pick any)");
        lblWeather.Text = T("天气", "Weather");
        lblBgNote.Text = T("背景跟着小窝一起移动，鼠标可以穿过它点到桌面。直接点温馨小屋里的落地灯也能开关。天气选「自动」时，会时不时下雨、打雷或下雪。",
                           "The background moves with the bed and lets clicks through. Click the floor lamp in the cozy room to switch it. On Auto, it sometimes rains, storms or snows.");
        for (int i = 0; i < sceneBtns.Count; i++)
        {
            NestLook li = L.Clone(); li.Bg = i;
            int bs = Math.Max(2, S(80) / BackdropForm.BC);
            Bitmap bmp = new Bitmap(BackdropForm.BC * bs, BackdropForm.BR * bs);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                if (i == 0) g.Clear(Color.FromArgb(245, 245, 248));
                BackdropForm.Draw(g, bs, li, 1, 0);
            }
            Image old = sceneBtns[i].Image; sceneBtns[i].Image = bmp; if (old != null) old.Dispose();
            sceneBtns[i].Text = en ? BackdropForm.SceneEn[i] : BackdropForm.SceneZh[i];
            Mark(sceneBtns[i], L.Bg == i);
        }
        for (int i = 0; i < wallBtns.Count; i++)
        {
            MarkSwatch(wallBtns[i], L.Wall == i);
            tip.SetToolTip(wallBtns[i], en ? BackdropForm.WallEn[i] : BackdropForm.WallZh[i]);
        }
        for (int i = 0; i < roomChks.Count; i++)
        {
            roomChks[i].Text = en ? BackdropForm.RoomEn[i] : BackdropForm.RoomZh[i];
            roomChks[i].FlatAppearance.BorderColor = roomChks[i].Checked ? Accent : Line;
        }
        for (int i = 0; i < outChks.Count; i++)
        {
            outChks[i].Text = en ? BackdropForm.OutEn[i] : BackdropForm.OutZh[i];
            outChks[i].FlatAppearance.BorderColor = outChks[i].Checked ? Accent : Line;
        }
        lblLight.Text = T("什么时候亮（天黑、下雨时会自动亮）", "When to light up (auto: at night or in bad weather)");
        string[] lz = { "关闭", "自动", "一直开" }, le = { "Off", "Auto", "Always" };
        for (int i = 0; i < lightBtns.Count; i++) { lightBtns[i].Text = en ? le[i] : lz[i]; Mark(lightBtns[i], L.Light == i); }
        chkLamp.Text = L.LampOn ? T("落地灯：开着", "Floor lamp: on") : T("落地灯：关着", "Floor lamp: off");
        chkLamp.FlatAppearance.BorderColor = L.LampOn ? Accent : Line;
        for (int i = 0; i < weatherBtns.Count; i++)
        {
            weatherBtns[i].Text = en ? BackdropForm.WeatherEn[i] : BackdropForm.WeatherZh[i];
            Mark(weatherBtns[i], L.Weather == i);
        }

        UpdateSnap();
        nestPreview.Invalidate();
    }

    void UpdateSnap()
    {
        if (snap != null) snap.Dispose();
        int sz = Math.Max(2, 2 * (preview.ClientSize.Width / 26));
        snap = pv.Snapshot(sz, (int[])pet.Outfit.Clone(), blinkT > 0);
        preview.Invalidate();
    }

    void PaintPreview(object sender, PaintEventArgs e)
    {
        if (snap == null) return;
        Graphics g = e.Graphics;
        int w = preview.ClientSize.Width, h = preview.ClientSize.Height;
        int sz = snap.Width / 12;
        int x = (w - snap.Width) / 2, y = (h - snap.Height) / 2 - sz / 2;
        using (Brush floor = new SolidBrush(Color.FromArgb(240, 226, 214)))
            g.FillRectangle(floor, 0, y + snap.Height, w, h - (y + snap.Height));
        using (Brush shadow = new SolidBrush(Color.FromArgb(50, 120, 70, 50)))
            g.FillEllipse(shadow, x + sz, y + snap.Height - sz / 2, 10 * sz, sz);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImageUnscaled(snap, x, y);
    }

    void PaintNest(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        PictureBox pb = (PictureBox)sender;
        int w = pb.ClientSize.Width, h = pb.ClientSize.Height;
        int sz = Math.Max(2, ((w - 8) / NestForm.NC) / 2 * 2);
        if (pet.Look.Bg > 0) sz = Math.Max(2, (w / BackdropForm.BC) / 2 * 2);   // 有背景时整体缩小一点
        int nw = NestForm.NC * sz, nh = NestForm.NR * sz;
        int x = (w - nw) / 2, y = (h - nh) / 2 + sz;
        if (pet.Look.Bg > 0)
        {
            int bw = BackdropForm.BC * sz, bh = BackdropForm.BR * sz;
            int bx = (w - bw) / 2, by = (h - bh) / 2;
            x = bx + (bw - nw) / 2; y = by + bh - nh;
            using (Bitmap bb = new Bitmap(bw, bh))
            {
                using (Graphics bg = Graphics.FromImage(bb)) BackdropForm.Draw(bg, sz, pet.Look, pet.Weather, zt * 4);
                ColorMatrix cm = new ColorMatrix(); cm.Matrix33 = pet.Look.BgAlpha / 100f;
                using (ImageAttributes ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    g.DrawImage(bb, new Rectangle(bx, by, bw, bh), 0, 0, bw, bh, GraphicsUnit.Pixel, ia);
                }
            }
        }
        else
            using (Brush floor = new SolidBrush(Color.FromArgb(240, 226, 214)))
                g.FillRectangle(floor, 0, y + nh, w, h - (y + nh));
        GraphicsState st = g.Save();
        g.TranslateTransform(x, y);
        NestForm.DrawNest(g, sz, pet.Look, pv, (int[])pet.Outfit.Clone());
        g.Restore(st);
        float dk = pet.PreviewDark;   // 预览里也把灯光叠上去，调滑块马上能看到
        if (dk > 0 && pet.NestOn)
        {
            NestLook L = pet.Look;
            string k = sz + "," + dk + "," + L.Bg + "," + L.GlowAlpha + "," + L.GlowSpread + "," + L.GlowSize + "," + L.GlowColor;
            if (k != glowPrevKey)
            {
                if (glowPrev != null) glowPrev.Dispose();
                NestLook l2 = L.Clone(); l2.RoomDecor &= ~128;
                glowPrev = LightForm.Build(sz, l2, dk, false, 0); glowPrevKey = k;
            }
            g.DrawImage(glowPrev, x + nw / 2 - glowPrev.Width / 2, y + nh - glowPrev.Height, glowPrev.Width, glowPrev.Height);
        }
        using (Brush zb = new SolidBrush(Color.FromArgb(120, 120, 150)))
            for (int i = 0; i < 3; i++)
                if ((zt / 5) % 4 >= i)
                    g.DrawString("z", zf, zb, x + 14 * sz + i * sz, y + (2 - i) * sz);
        if (!pet.NestOn)
            using (Brush veil = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
                g.FillRectangle(veil, 0, 0, w, h);
    }
}
