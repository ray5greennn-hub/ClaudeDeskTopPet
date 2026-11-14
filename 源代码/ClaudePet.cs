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
        Application.Run(new PetForm());
    }
}

enum St { Fall, Jump, Walk, Idle, Sleep, Computer, Cling, Dance, Bubble, Stretch, Eat, Bye, GoNest, Gift, Play, Bath, Frozen }

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
    int foodIdx, bites, eatTick; float foodY;
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
    int quitT; bool sleepyBye, bathBye;
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
                + "\nplushL=" + Look.PlushL + "\nplushR=" + Look.PlushR;
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
        if (bath != null) bath.Invalidate();
    }

    internal void SetNestEnabled(bool on)
    {
        if (NestOn == on) return;
        NestOn = on;
        if (on) ShowNest(true); else HideNest();
        SaveConfig(); RefreshSettings();
    }
    internal void NestLookChanged() { SaveConfig(); if (nest != null) nest.Invalidate(); RefreshSettings(); }
    internal void GoSleepNow() { GoSleep(); }

    // 只用于预览：用指定大小、指定装扮画一只站着的 Claude
    internal Bitmap Snapshot(int sz, int[] fit, bool closedEyes)
    {
        Bitmap bmp = new Bitmap(COLS * sz, ROWS * sz);
        using (Graphics g = Graphics.FromImage(bmp)) DrawPose(g, sz, fit, St.Idle, closedEyes);
        return bmp;
    }
    internal void DrawSleeper(Graphics g, int sz, int[] fit) { DrawPose(g, sz, fit, St.Sleep, false); }
    internal void DrawBather(Graphics g, int sz, int[] fit, bool closedEyes) { DrawPose(g, sz, fit, St.Idle, closedEyes); }

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
    static readonly string[] FoodZh = { "苹果", "饼干", "蛋糕", "寿司", "披萨", "咖啡", "辣椒", "冰淇淋", "西瓜", "甜甜圈", "汉堡", "棒棒糖", "牛奶", "柠檬", "爆米花", "薄荷糖" };
    static readonly string[] FoodEn = { "Apple", "Cookie", "Cake", "Sushi", "Pizza", "Coffee", "Chili", "Ice cream", "Watermelon", "Donut", "Burger", "Lollipop", "Milk", "Lemon", "Popcorn", "Mint candy" };
    const int COFFEE = 5, CHILI = 6, CAKE = 2, ICE = 7, WATERMELON = 8, DONUT = 9, BURGER = 10, LOLLI = 11, MILK = 12, LEMON = 13, POPCORN = 14, MINT = 15;
    static readonly string[][] Foods = {
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
        for (int i = 0; i < Foods.Length; i++)
        {
            int idx = i;
            feed.DropDownItems.Add(En ? FoodEn[i] : FoodZh[i], ArtIcon(Foods[i], 2), delegate { Feed(idx); });
        }
        menu.Items.Add(feed);

        ToolStripMenuItem act = new ToolStripMenuItem(T("动作", "Actions"));
        act.DropDownItems.Add(T("跳一下 ♥", "Jump ♥"), null, delegate { Poke(); });
        act.DropDownItems.Add(T("玩电脑", "Use the computer"), null, delegate { Do(St.Computer, 700); });
        act.DropDownItems.Add(T("跳舞", "Dance"), null, delegate { Do(St.Dance, 300); });
        act.DropDownItems.Add(T("吹泡泡", "Blow bubbles"), null, delegate { Do(St.Bubble, 300); });
        act.DropDownItems.Add(T("伸懒腰", "Stretch"), null, delegate { Do(St.Stretch, 46); });
        act.DropDownItems.Add(T("去睡觉 / 叫醒", "Sleep / Wake up"), null, delegate
        {
            if (state == St.Sleep) Wake(); else GoSleep();
        });
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
        if (bath != null) bath.Close();
        base.OnFormClosed(e);
    }

    // 让桌宠盖在小窝 / 浴缸上面，玩具（附属窗口）会自动在桌宠上面
    void RaiseZ()
    {
        if (!IsHandleCreated) return;
        SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
        if (toy != null && !toy.IsDisposed && toy.IsHandleCreated) SetWindowPos(toy.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x13);
    }

    bool OnGround()
    {
        return state == St.Walk || state == St.Idle || state == St.Sleep || state == St.Computer
            || state == St.Dance || state == St.Bubble || state == St.Stretch || state == St.Eat
            || state == St.GoNest || state == St.Gift || state == St.Play || state == St.Frozen;
    }
    bool Busy() { return state == St.Bye || inBath || intoBath || quitT > 0 || state == St.Frozen; }

    // ---------- 行为 ----------
    void Do(St s, int t)
    {
        if (Busy()) return;
        if (state == St.Sleep) { Wake(); return; }
        if (!OnGround()) return;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        state = s; timer = t; frame = 0;
    }

    void Pick()
    {
        if (toy != null && rand.Next(100) < 75) { state = St.Play; playTimer = rand.Next(400, 900); return; }
        int r = rand.Next(100);
        if (r < 38) { state = St.Walk; dir = rand.Next(2) == 0 ? 1 : -1; timer = rand.Next(60, 220); }
        else if (r < 53) { state = St.Idle; timer = rand.Next(40, 140); }
        else if (r < 67) { state = St.Computer; timer = rand.Next(300, 700); frame = 0; }
        else if (r < 76) { state = St.Dance; timer = rand.Next(150, 300); frame = 0; }
        else if (r < 84) { state = St.Bubble; timer = rand.Next(150, 300); frame = 0; }
        else if (r < 92) { state = St.Stretch; timer = 46; }
        else { state = St.Idle; GoSleep(); }
    }

    bool NestUsable()
    {
        if (nest == null || nest.IsDisposed || !nest.Visible) return false;
        Point pc = new Point((int)(px + Width / 2), (int)(py + Height / 2));
        Point nc = new Point((int)(nest.x + nest.Width / 2), (int)(nest.y + nest.Height / 2));
        return Screen.FromPoint(pc).Bounds == Screen.FromPoint(nc).Bounds;
    }
    float NestPetX() { return nest.x + (nest.Width - Width) / 2f; }

    // 睡觉：有小窝就走过去钻进去，没有就原地睡；只有被点一下才会醒
    void GoSleep()
    {
        if (Busy() || state == St.Sleep || !OnGround()) return;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        if (NestUsable()) { state = St.GoNest; timer = 900; }
        else { state = St.Sleep; frame = 0; }
    }

    void Wake()
    {
        if (inNest) { WakeFromNest(); return; }
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
        RaiseZ();
    }

    void HideNest()
    {
        inNest = false; intoNest = false;   // 在窝里睡着的话，就地继续睡
        if (state == St.GoNest) { state = St.Idle; timer = 40; }
        if (nest != null) nest.Hide();
    }

    internal void OnNestMoved()
    {
        if (inNest)
        {
            px = NestPetX(); py = nest.y;
            Location = new Point((int)px, (int)py);
        }
        RaiseZ();
    }
    internal void OnNestDropped() { SaveConfig(); RaiseZ(); }
    internal void OnNestClicked() { if (inNest) WakeFromNest(); RaiseZ(); }

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
        if (!pinned && r < 12) { HopOff(); return; }
        if (r < 45) { vdir = -1; timer = rand.Next(30, 120); }
        else if (r < 72) { vdir = 1; timer = rand.Next(30, 120); }
        else { vdir = 0; timer = rand.Next(40, 160); }
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
        if (state == St.Sleep) { Wake(); return; }
        if (state == St.Cling) { HopOff(); happy = 30; AddHearts(3); }
        else if (OnGround()) { state = St.Jump; vy = -11f * K; vx = 0; happy = 30; AddHearts(3); }
    }

    void SayBye()
    {
        if (state == St.Bye || quitT > 0) return;
        if (settings != null && !settings.IsDisposed) settings.Close();
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
        if (state != St.Cling) side = 0;
        state = St.Bye; timer = 60; frame = 0; vdir = 0;
        AddText(T("拜拜~", "Bye~"), PinkCol, tfont);
        RaiseZ();
    }

    void Feed(int idx)
    {
        if (Busy()) return;
        if (state == St.Sleep) { Wake(); return; }
        if (!OnGround()) return;
        if (state == St.Gift && giftPhase == 0) giftPending = true;
        if (full >= 6)   // 吃撑了，摇头拒绝
        {
            state = St.Idle; timer = 70; shake = 36;
            AddText(T("饱啦~", "So full~"), PinkCol, tfont);
            return;
        }
        state = St.Eat; foodIdx = idx; foodY = -8 * FoodPx(); bites = 0; eatTick = 0; frame = 0;
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
        giftPhase = 1; timer = 160; happy = 60;
        AddHearts(4);
        for (int k = 0; k < 8; k++)
            AddSpark(rand.Next(SZ, 11 * SZ), rand.Next(SZ, 6 * SZ), k % 2 == 0 ? Yellow : PinkCol, 18);
        AddText(En ? "For you!" : "送你" + GiftZh[giftIdx] + "~", PinkCol, tfont);
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
        if (state == St.Sleep) Wake();
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
            if (bath != null && !bath.IsDisposed) bath.Opacity = op;
            if (quitT == 0) { timerCtl.Stop(); Close(); return; }
        }
        if (nest != null && !nest.IsDisposed && nest.Visible) nest.Step();
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
        if (tick % 150 == 0 && ((nest != null && nest.Visible) || bath != null)) RaiseZ();

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
                    if (tick % 6 == 0) nest.Invalidate();   // 在窝里也会慢慢呼吸
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
                if (--timer <= 0) Pick();
                break;
            case St.Sleep:   // 深度睡眠：不会自己醒
                if (tick % 15 == 0) frame++;
                break;
            case St.GoNest:
                if (!NestUsable()) { state = St.Sleep; frame = 0; break; }
                if (nest.drag || nest.y < nest.Ground() - 1) break;   // 窝还在空中，等一下
                {
                    float tx = NestPetX(), d = tx - px;
                    if (Math.Abs(d) <= 3f * K) { px = tx; state = St.Jump; vy = -8f * K; vx = 0; intoNest = true; }
                    else
                    {
                        dir = d > 0 ? 1 : -1; px += dir * 2f * K;
                        if (tick % 6 == 0) frame++;
                        if (--timer <= 0) { state = St.Sleep; frame = 0; }
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
                else if (timer <= 0) { state = St.Idle; timer = 40; }
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
                float rest = (TOP + 4) * SZ;
                if (foodY < rest) { foodY = Math.Min(rest, foodY + 3f * K); }
                else
                {
                    eatTick++;
                    if (eatTick % 5 == 0) frame++;
                    if (eatTick % 14 == 0)
                    {
                        bites++;
                        AddSpark(5 * SZ, (TOP + 4) * SZ, Color.FromArgb(240, 220, 170), 8);
                        AddSpark(6 * SZ, (TOP + 4) * SZ, Color.FromArgb(240, 220, 170), 8);
                        if (bites >= 4) FinishEat();
                    }
                }
                break;
            case St.Bye:
                if (tick % 5 == 0) frame++;
                timer--;
                if (timer < 12)
                {
                    Opacity = Math.Max(0.0, timer / 12.0);
                    if (nest != null && !nest.IsDisposed && nest.Visible) nest.Opacity = Opacity;
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
        if (inNest) { WakeFromNest(); return; }
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

        if (state == St.Eat && !drag)
        {
            int fp = FoodPx();
            DrawArt(g, Foods[foodIdx], (COLS * SZ - 8 * fp) / 2, (int)foodY, fp, Math.Min(8, bites * 2));
        }
        if (state == St.Gift && giftPhase == 1 && !drag)
        {
            int fp = SZ / 2, bob = (tick / 8) % 2 == 0 ? 0 : SZ / 4;
            DrawArt(g, Gifts[giftIdx], (COLS * SZ - 8 * fp) / 2, (TOP - 4) * SZ - SZ / 2 - bob, fp, 0);
        }

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
        bool walking = (!drag && (state == St.Walk || state == St.GoNest || (state == St.Play && chasing))) || climbing;
        bool dance = state == St.Dance && !drag;
        bool bubble = state == St.Bubble && !drag;
        bool stretch = state == St.Stretch && !drag && timer > 8 && timer < 38;
        bool eat = state == St.Eat && !drag;
        bool bye = state == St.Bye || bathBye;
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
        if ((shiver > 0 || sourT > 0) && !dance && !drag) ox = (tick / 2) % 2 == 0 ? q : -q;   // 冷得 / 酸得发抖

        // 身体起伏（像素级）：呼吸、走路颠、落地压扁、起跳/伸懒腰拉长
        double bobF = 0, lift = 0;
        if (!drag && !airborne && !dance)
        {
            if (state == St.Idle || state == St.Eat || state == St.Gift || state == St.Bubble)
                bobF = (1 - Math.Cos(TAU * ((tick * 5 / 56) % 5) / 5)) / 2 * half;
            if (state == St.Sleep) bobF = (1 - Math.Cos(TAU * ((tick / 18) % 5) / 5)) / 2 * half;
            if (walking) bobF = new[] { 2, 1, 0, 1, 0 }[ph8] * q / 2.0;
        }
        if (squash > 0) bobF = squash * SZ / 5.0;
        if (rising) lift = Math.Round(Math.Min(half, -vy / (6f * K) * half) / q) * q;
        if (stretchAmt > 0) lift = stretchAmt * SZ;
        int bobPx = (int)Math.Round(bobF - lift), legStretch = (int)Math.Round(lift);

        if (outfit[3] == 6) { oy += bobPx; H(g, coatCape, 2, B + 3, 20, 9); oy -= bobPx; }   // 披风在身后

        // 腿（不跟着身体起伏，身体压下来就像蹲了一下）
        int[] legs = { 2, 4, 7, 9 };
        int[] liftA = { 0, 2, 4, 0, 0 }, liftB = { 0, 0, 0, 4, 2 };
        int kickLeg = dir > 0 ? 3 : 0;
        for (int i = 0; i < 4; i++)
        {
            int lp = 0;
            if (walking) lp = (i % 2 == 0 ? liftA : liftB)[ph8] * q;
            if (held) lp = (int)Math.Round((1 + Math.Sin(TAU * ((tick / 2 + i) % 5) / 5)) / 2 * SZ);   // 被拎起来蹬腿
            if (kicking && i == kickLeg) lp = SZ;
            if (falling) lp = half;
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
        if (dance) { int s = (int)Math.Round(Math.Sin(TAU * d8 / 5) * SZ); laPx = -s; raPx = s; }
        if (walking && !climbing) { int s = (int)Math.Round(Math.Sin(TAU * ph8 / 5) * q); laPx += s; raPx -= s; }   // 走路时手前后摆
        if (bye) { ra = B - 2; raPx = (int)Math.Round(Math.Sin(TAU * ((tick / 2) % 5) / 5) * half); }
        if (sleepyBye) { ra = B + 1; raPx = (int)Math.Round(Math.Sin(TAU * ((tick / 5) % 5) / 5) * q); }   // 迷迷糊糊慢慢挥手          // 举高小手挥一挥
        Block(g, armL, 0, la, 4, 4, laPx);
        Block(g, armR, 20, ra, 4, 4, raPx);

        int e1 = 3, e2 = 8, ld = 0;
        if ((state == St.Walk || state == St.GoNest || state == St.Play) && !drag) ld = dir;
        if (state == St.Idle && !drag && cs == 0 && shake == 0) ld = lookDir;   // 发呆时眼睛跟着鼠标
        if (climbing) ld = cs < 0 ? vdir : -vdir;
        if (ld > 0) { e1 = 4; e2 = 8; }
        if (ld < 0) { e1 = 3; e2 = 7; }
        double open = (blink >= 1 && blink <= 6) ? BlinkOpen[blink] : 1.0;   // 眨眼：半闭 → 闭上 → 半闭 → 睁开
        if (frozen) open = 1.0;
        bool closed = !held && !frozen && ((state == St.Sleep && !drag) || open <= 0 || happy > 0
            || dance || stretch || bye || shake > 0 || giftShow || sourT > 0);
        if (sleepyBye) { closed = false; open = 0.5; }   // 半睁着眼
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
        if (eat && foodY >= (TOP + 4) * SZ)                                              // 嚼嚼嚼（5 帧）
        {
            int mh = half + (int)Math.Round((1 + Math.Sin(TAU * ((eatTick / 2) % 5) / 5)) / 2 * half);
            R(g, dark, 5 * SZ, (b + 3) * SZ - half, 2 * SZ, mh);
        }

        DrawAcc(g, B, B, B + 2, 0, eyes);
        accDx = 0;
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
        oy = oy0;
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

    // ---------- 自检：把各种画面画成图 ----------
    public static void Render(string path)
    {
        PetForm f = new PetForm(true);
        int W = f.ClientSize.Width, pad = 12, n = 35, perRow = 5, S = f.SZ;
        using (Bitmap bmp = new Bitmap(Math.Max(pad + perRow * (W + pad), 16 * 30 + pad), pad + 8 * (W + pad)))
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
                    f.iceHP = 3; f.shiver = 0; f.bloat = 0; f.milkT = 0; f.sourT = 0; f.sleepyBye = false; f.bathBye = false; f.vy = 0;
                    f.state = St.Idle; f.outfit = new int[SLOTS];
                    int k8 = i % 5;   // 每一行是同一个动作的 5 帧
                    switch (i / 5)
                    {
                        case 0: f.state = St.Walk; f.tick = (k8 * 16 + 4) / 5; break;                       // 走路
                        case 1: f.state = St.Dance; f.tick = (k8 * 24 + 4) / 5; break;                      // 跳舞
                        case 2: f.state = St.Bye; f.tick = 2 * k8; break;             // 挥手
                        case 3: f.state = St.Computer; f.tick = 2 * k8; break;        // 打字
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
                    }
                    g.TranslateTransform(pad + (i % perRow) * (W + pad), pad + (i / perRow) * (W + pad));
                    f.Draw(g);
                    g.ResetTransform();
                }
                // 最后两行：6 种玩具 + 16 种食物
                int ty0 = pad + 7 * (W + pad);
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
        pet.Look.Type = 0; pet.Look.Decor = 1 | 2 | 8 | 32 | 64; pet.Look.PlushL = 1; pet.Look.PlushR = 6; pet.Look.Pattern = 1; pet.Look.FlagColor = 4;
        int[] tabsToShoot = { 0, 0, 1, 1, 1, 1 };
        int[] scrolls = { 0, 9999, 0, 380, 9999, 9999 };
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
                g.TranslateTransform(6 * sz, 0);
                sleeper.DrawSleeper(g, sz, fit);
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
            if (sleeper != null && PawsAt[t])
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
    readonly Button btnBed = new Button();
    readonly Label lblDecor = new Label(), lblFlag = new Label(), lblFlagNote = new Label();
    readonly List<CheckBox> decorChks = new List<CheckBox>();
    readonly List<Button> flagBtns = new List<Button>();
    readonly Label lblPlushL = new Label(), lblPlushR = new Label(), lblPlushNote = new Label();
    readonly List<Button> plushLBtns = new List<Button>(), plushRBtns = new List<Button>();

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
            if (++zt % 5 == 0) nestPreview.Invalidate();
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
        lblHint.AutoSize = false;
        lblHint.SetBounds(S(8), S(280), S(205), S(110));
        lblHint.ForeColor = Color.FromArgb(110, 110, 120);
        tNest.Controls.Add(lblHint);

        Panel P = ScrollPanel(tNest, nestPanel);

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
        Spacer(P, 0, S(764));
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
        lblHint.Text = T("· 右边可以用滚轮往下翻：装饰、小旗子颜色、玩偶都在下面\n· 小窝可以在桌面上随意拖动\n· 点一下小窝里的 Claude 才会把它叫醒",
                         "· Scroll down on the right for decorations, flag color and plushies\n· Drag the bed anywhere on the desktop\n· Click Claude in the bed to wake it up");
        btnBed.Text = T("现在去小窝睡觉 Zz", "Go to bed now Zz");
        btnBed.Enabled = pet.NestOn;
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
        int nw = NestForm.NC * sz, nh = NestForm.NR * sz;
        int x = (w - nw) / 2, y = (h - nh) / 2 + sz;
        using (Brush floor = new SolidBrush(Color.FromArgb(240, 226, 214)))
            g.FillRectangle(floor, 0, y + nh, w, h - (y + nh));
        GraphicsState st = g.Save();
        g.TranslateTransform(x, y);
        NestForm.DrawNest(g, sz, pet.Look, pv, (int[])pet.Outfit.Clone());
        g.Restore(st);
        using (Brush zb = new SolidBrush(Color.FromArgb(120, 120, 150)))
            for (int i = 0; i < 3; i++)
                if ((zt / 5) % 4 >= i)
                    g.DrawString("z", zf, zb, x + 14 * sz + i * sz, y + (2 - i) * sz);
        if (!pet.NestOn)
            using (Brush veil = new SolidBrush(Color.FromArgb(160, 255, 255, 255)))
                g.FillRectangle(veil, 0, 0, w, h);
    }
}
