param(
    [string]$OutRoot = "C:\Users\Nelson\.gemini\config\plugins\antigravity-pet\pets"
)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

public static class CodexPetGenerator
{
    private const int GridW = 36;
    private const int GridH = 39;
    private const int Px = 5;    // 36*5 = 180, 39*5 = 195 -> centered inside 192x208
    private const int PadX = 6;
    private const int PadY = 6;
    private const int CellW = 192;
    private const int CellH = 208;
    private const int Cols = 8;
    private const int Rows = 11;

    private static void SetPix(Color[,] grid, int x, int y, Color c)
    {
        if (x >= 0 && x < GridW && y >= 0 && y < GridH) grid[x, y] = c;
    }

    private static void FillRect(Color[,] grid, int x0, int y0, int w, int h, Color c)
    {
        for (int x = x0; x < x0 + w; x++)
            for (int y = y0; y < y0 + h; y++)
                SetPix(grid, x, y, c);
    }

    private static void ClearPix(Color[,] grid, int x, int y)
    {
        if (x >= 0 && x < GridW && y >= 0 && y < GridH) grid[x, y] = Color.Empty;
    }

    private static void AddOutline(Color[,] grid, Color outline)
    {
        var copy = (Color[,])grid.Clone();
        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                if (copy[x, y].A != 0) continue;
                for (int i = 0; i < 4; i++)
                {
                    int nx = x + dx[i], ny = y + dy[i];
                    if (nx >= 0 && nx < GridW && ny >= 0 && ny < GridH && copy[nx, ny].A > 0)
                    {
                        grid[x, y] = outline;
                        break;
                    }
                }
            }
        }
    }

    private static void DrawSymbol(Color[,] grid, string sym, int sx, int sy, Color c)
    {
        if (sym == "?")
        {
            int[,] pts = { {1,0},{2,0},{3,0},{0,1},{3,1},{3,2},{2,3},{2,4},{2,6} };
            for (int i = 0; i < pts.GetLength(0); i++) SetPix(grid, sx + pts[i,0], sy + pts[i,1], c);
        }
        else if (sym == "X")
        {
            int[,] pts = { {0,0},{3,0},{1,1},{2,1},{1,2},{2,2},{0,3},{3,3} };
            for (int i = 0; i < pts.GetLength(0); i++) SetPix(grid, sx + pts[i,0], sy + pts[i,1], c);
        }
        else if (sym == "star")
        {
            int[,] pts = { {2,0},{2,1},{0,2},{1,2},{2,2},{3,2},{4,2},{2,3},{2,4} };
            for (int i = 0; i < pts.GetLength(0); i++) SetPix(grid, sx + pts[i,0], sy + pts[i,1], c);
        }
    }

    private static Color[,] RenderFrame(string skin, string pose, int frame, int lookDir)
    {
        var grid = new Color[GridW, GridH];
        Color outline = Color.FromArgb(255, 15, 23, 42);
        Color white = Color.FromArgb(255, 248, 250, 252);
        Color cream = Color.FromArgb(255, 241, 245, 249);
        Color blush = Color.FromArgb(255, 244, 114, 182);
        Color gold = Color.FromArgb(255, 250, 204, 21);
        Color amber = Color.FromArgb(255, 245, 158, 11);
        Color red = Color.FromArgb(255, 239, 68, 68);
        Color green = Color.FromArgb(255, 52, 211, 153);
        Color cyan = Color.FromArgb(255, 56, 189, 248);

        int bodyY = 0, bodyX = 0, tailLift = 0, legStep = 0;
        bool blink = false, happyEye = false, dizzyEye = false, waveArm = false, typing = false;
        int eyeDx = 0, eyeDy = 0;

        switch (pose)
        {
            case "idle":
                if (frame == 2 || frame == 3) bodyY = 1;
                if (frame == 5) blink = true;
                if (frame == 1 || frame == 4) tailLift = -1;
                break;
            case "run":
                legStep = frame % 4;
                if (legStep == 1 || legStep == 3) bodyY = -1;
                tailLift = (frame % 2 == 0) ? -1 : 1;
                eyeDx = 1;
                break;
            case "wave":
                waveArm = true;
                happyEye = true;
                if (frame % 2 == 1) bodyY = -1;
                break;
            case "jump":
                int[] jOff = { 1, -2, -5, -2, 0 };
                bodyY = jOff[frame % jOff.Length];
                if (frame == 2) happyEye = true;
                break;
            case "failed":
                dizzyEye = true;
                bodyX = (frame % 2 == 0) ? -1 : 1;
                tailLift = 1;
                break;
            case "waiting":
                if (frame % 2 == 1) bodyY = -1;
                eyeDy = -1;
                break;
            case "work":
                typing = true;
                if (frame % 2 == 1) bodyY = 1;
                eyeDy = 1;
                break;
            case "review":
                happyEye = true;
                waveArm = true;
                int[] rOff = { 0, -2, -4, -1, 0, -2 };
                bodyY = rOff[frame % rOff.Length];
                break;
            case "look":
                double rad = (lookDir * 22.5) * Math.PI / 180.0;
                eyeDx = (int)Math.Round(Math.Cos(rad));
                eyeDy = (int)Math.Round(Math.Sin(rad));
                break;
        }

        int cx = 18 + bodyX;
        int cy = 22 + bodyY;

        if (skin == "cyber-fox")
        {
            Color main = Color.FromArgb(255, 56, 189, 248);
            Color shade = Color.FromArgb(255, 2, 132, 199);
            Color earIn = Color.FromArgb(255, 186, 230, 253);

            // 尾巴（带像素倒角）
            int ty = cy + 5 + tailLift;
            FillRect(grid, cx - 13, ty - 2, 6, 5, main);
            FillRect(grid, cx - 15, ty - 5, 4, 4, cream);
            ClearPix(grid, cx - 15, ty - 5);
            FillRect(grid, cx - 9, ty + 2, 2, 2, shade);

            // 身体与白肚皮
            FillRect(grid, cx - 6, cy + 2, 12, 8, main);
            FillRect(grid, cx - 3, cy + 3, 6, 7, cream);

            // 脚
            int lf = cy + 10, rf = cy + 10;
            if (pose == "run") { if (legStep == 0) lf -= 2; else if (legStep == 2) rf -= 2; }
            FillRect(grid, cx - 6, lf, 3, 3, cream);
            FillRect(grid, cx + 3, rf, 3, 3, cream);

            // 头部（方正 Q 萌比例 + 四角 1px 倒角）
            FillRect(grid, cx - 8, cy - 9, 16, 10, main);
            FillRect(grid, cx - 9, cy - 6, 18, 6, main);
            ClearPix(grid, cx - 8, cy - 9);
            ClearPix(grid, cx + 7, cy - 9);

            // 白脸颊
            FillRect(grid, cx - 9, cy - 3, 6, 4, cream);
            FillRect(grid, cx + 3, cy - 3, 6, 4, cream);
            FillRect(grid, cx - 3, cy - 2, 6, 3, cream);

            // 尖耳与内耳
            FillRect(grid, cx - 8, cy - 13, 4, 4, main);
            FillRect(grid, cx - 7, cy - 15, 2, 2, main);
            FillRect(grid, cx - 6, cy - 12, 2, 3, earIn);

            FillRect(grid, cx + 4, cy - 13, 4, 4, main);
            FillRect(grid, cx + 5, cy - 15, 2, 2, main);
            FillRect(grid, cx + 4, cy - 12, 2, 3, earIn);

            // 小鼻子
            FillRect(grid, cx - 1, cy - 2, 2, 1, outline);
        }
        else if (skin == "astro-bot")
        {
            Color suit = Color.FromArgb(255, 241, 245, 249);
            Color suitShade = Color.FromArgb(255, 203, 213, 225);
            Color visor = Color.FromArgb(255, 15, 23, 42);
            Color pack = Color.FromArgb(255, 56, 189, 248);

            FillRect(grid, cx - 1, cy - 13, 2, 3, suitShade);
            FillRect(grid, cx - 1, cy - 15, 2, 2, cyan);
            FillRect(grid, cx - 9, cy + 2, 18, 7, pack);
            FillRect(grid, cx - 6, cy + 2, 12, 8, suit);
            FillRect(grid, cx - 3, cy + 4, 6, 3, visor);
            SetPix(grid, cx - 2, cy + 5, cyan);
            SetPix(grid, cx + 1, cy + 5, green);

            int lf = cy + 10, rf = cy + 10;
            if (pose == "run") { if (legStep == 0) lf -= 2; else if (legStep == 2) rf -= 2; }
            FillRect(grid, cx - 6, lf, 4, 3, suitShade);
            FillRect(grid, cx + 2, rf, 4, 3, suitShade);

            FillRect(grid, cx - 8, cy - 10, 16, 11, suit);
            FillRect(grid, cx - 9, cy - 7, 18, 7, suit);
            ClearPix(grid, cx - 8, cy - 10);
            ClearPix(grid, cx + 7, cy - 10);
            FillRect(grid, cx - 6, cy - 7, 12, 6, visor);
        }
        else if (skin == "shadow-cat")
        {
            Color fur = Color.FromArgb(255, 38, 41, 54);
            Color furHi = Color.FromArgb(255, 58, 63, 82);
            Color pink = Color.FromArgb(255, 244, 114, 182);

            int ty = cy + 4 + tailLift;
            FillRect(grid, cx + 7, ty, 5, 3, fur);
            FillRect(grid, cx + 10, ty - 5, 3, 6, fur);
            FillRect(grid, cx + 9, ty - 6, 2, 2, furHi);

            FillRect(grid, cx - 6, cy + 2, 12, 8, fur);
            FillRect(grid, cx - 3, cy + 4, 6, 5, furHi);

            int lf = cy + 10, rf = cy + 10;
            if (pose == "run") { if (legStep == 0) lf -= 2; else if (legStep == 2) rf -= 2; }
            FillRect(grid, cx - 6, lf, 3, 3, fur);
            FillRect(grid, cx + 3, rf, 3, 3, fur);

            FillRect(grid, cx - 8, cy - 9, 16, 10, fur);
            FillRect(grid, cx - 9, cy - 6, 18, 6, fur);
            ClearPix(grid, cx - 8, cy - 9);
            ClearPix(grid, cx + 7, cy - 9);

            FillRect(grid, cx - 8, cy - 13, 4, 4, fur);
            FillRect(grid, cx - 7, cy - 14, 2, 2, fur);
            FillRect(grid, cx - 6, cy - 12, 2, 2, pink);

            FillRect(grid, cx + 4, cy - 13, 4, 4, fur);
            FillRect(grid, cx + 5, cy - 14, 2, 2, fur);
            FillRect(grid, cx + 4, cy - 12, 2, 2, pink);

            FillRect(grid, cx - 1, cy - 2, 2, 1, pink);
        }
        else if (skin == "shiba-dog")
        {
            Color tan = Color.FromArgb(255, 245, 158, 11);
            Color tanHi = Color.FromArgb(255, 251, 191, 36);
            Color earDark = Color.FromArgb(255, 180, 83, 9);

            int ty = cy + 3 + tailLift;
            FillRect(grid, cx - 12, ty - 1, 6, 4, tan);
            FillRect(grid, cx - 13, ty - 4, 4, 4, tanHi);
            FillRect(grid, cx - 11, ty - 3, 2, 2, cream);

            FillRect(grid, cx - 6, cy + 2, 12, 8, tan);
            FillRect(grid, cx - 3, cy + 3, 6, 7, cream);

            int lf = cy + 10, rf = cy + 10;
            if (pose == "run") { if (legStep == 0) lf -= 2; else if (legStep == 2) rf -= 2; }
            FillRect(grid, cx - 6, lf, 3, 3, cream);
            FillRect(grid, cx + 3, rf, 3, 3, cream);

            FillRect(grid, cx - 8, cy - 9, 16, 10, tan);
            FillRect(grid, cx - 9, cy - 6, 18, 6, tan);
            ClearPix(grid, cx - 8, cy - 9);
            ClearPix(grid, cx + 7, cy - 9);

            FillRect(grid, cx - 9, cy - 3, 6, 4, cream);
            FillRect(grid, cx + 3, cy - 3, 6, 4, cream);
            FillRect(grid, cx - 3, cy - 2, 6, 3, cream);

            FillRect(grid, cx - 8, cy - 13, 4, 4, tan);
            FillRect(grid, cx - 6, cy - 12, 2, 2, earDark);
            FillRect(grid, cx + 4, cy - 13, 4, 4, tan);
            FillRect(grid, cx + 4, cy - 12, 2, 2, earDark);

            // 豆豆眉
            FillRect(grid, cx - 5, cy - 8, 2, 1, cream);
            FillRect(grid, cx + 3, cy - 8, 2, 1, cream);
            FillRect(grid, cx - 1, cy - 2, 2, 1, outline);
        }
        else if (skin == "pixel-dragon")
        {
            Color emerald = Color.FromArgb(255, 16, 185, 129);
            Color jade = Color.FromArgb(255, 5, 150, 105);
            Color wing = Color.FromArgb(255, 110, 231, 183);

            int ty = cy + 4 + tailLift;
            FillRect(grid, cx - 12, ty, 6, 3, emerald);
            FillRect(grid, cx - 14, ty - 2, 3, 3, jade);
            FillRect(grid, cx - 9, cy + 1, 3, 3, wing);
            FillRect(grid, cx + 6, cy + 1, 3, 3, wing);

            FillRect(grid, cx - 6, cy + 2, 12, 8, emerald);
            FillRect(grid, cx - 3, cy + 3, 6, 7, gold);

            int lf = cy + 10, rf = cy + 10;
            if (pose == "run") { if (legStep == 0) lf -= 2; else if (legStep == 2) rf -= 2; }
            FillRect(grid, cx - 6, lf, 3, 3, jade);
            FillRect(grid, cx + 3, rf, 3, 3, jade);

            FillRect(grid, cx - 8, cy - 9, 16, 10, emerald);
            FillRect(grid, cx - 9, cy - 6, 18, 6, emerald);
            ClearPix(grid, cx - 8, cy - 9);
            ClearPix(grid, cx + 7, cy - 9);

            FillRect(grid, cx - 7, cy - 13, 3, 4, gold);
            SetPix(grid, cx - 7, cy - 14, amber);
            FillRect(grid, cx + 4, cy - 13, 3, 4, gold);
            SetPix(grid, cx + 6, cy - 14, amber);

            FillRect(grid, cx - 7, cy - 2, 2, 1, blush);
            FillRect(grid, cx + 5, cy - 2, 2, 1, blush);
            SetPix(grid, cx - 2, cy - 2, jade);
            SetPix(grid, cx + 1, cy - 2, jade);
        }
        else // byte-ghost
        {
            Color gBody = Color.FromArgb(255, 238, 242, 255);
            Color gShade = Color.FromArgb(255, 199, 210, 254);
            outline = Color.FromArgb(255, 99, 102, 241);

            FillRect(grid, cx - 8, cy - 9, 16, 18, gBody);
            FillRect(grid, cx - 9, cy - 6, 18, 13, gBody);
            FillRect(grid, cx - 5, cy - 11, 10, 2, gBody);
            ClearPix(grid, cx - 8, cy - 9);
            ClearPix(grid, cx + 7, cy - 9);

            int ws = frame % 2;
            FillRect(grid, cx - 8 + ws, cy + 9, 3, 3, gShade);
            FillRect(grid, cx - 2, cy + 9, 4, 3, gBody);
            FillRect(grid, cx + 5 - ws, cy + 9, 3, 3, gShade);
            FillRect(grid, cx - 7, cy - 2, 3, 2, blush);
            FillRect(grid, cx + 4, cy - 2, 3, 2, blush);
        }

        // 经典方正像素眼（4x4 眼白 + 3x3 瞳孔 + 左上 1x1 高光）
        Color eyeBg = (skin == "astro-bot") ? Color.FromArgb(255, 15, 23, 42) : white;
        Color pupil = (skin == "astro-bot") ? cyan : (skin == "shadow-cat" ? gold : outline);
        int ey = cy - 7;

        if (blink)
        {
            FillRect(grid, cx - 6, ey + 2, 4, 1, pupil);
            FillRect(grid, cx + 2, ey + 2, 4, 1, pupil);
        }
        else if (happyEye)
        {
            SetPix(grid, cx - 6, ey + 2, pupil); FillRect(grid, cx - 5, ey + 1, 2, 1, pupil); SetPix(grid, cx - 3, ey + 2, pupil);
            SetPix(grid, cx + 2, ey + 2, pupil); FillRect(grid, cx + 3, ey + 1, 2, 1, pupil); SetPix(grid, cx + 5, ey + 2, pupil);
        }
        else if (dizzyEye)
        {
            SetPix(grid, cx - 6, ey, red); SetPix(grid, cx - 3, ey, red); FillRect(grid, cx - 5, ey + 1, 2, 2, red); SetPix(grid, cx - 6, ey + 3, red); SetPix(grid, cx - 3, ey + 3, red);
            SetPix(grid, cx + 2, ey, red); SetPix(grid, cx + 5, ey, red); FillRect(grid, cx + 3, ey + 1, 2, 2, red); SetPix(grid, cx + 2, ey + 3, red); SetPix(grid, cx + 5, ey + 3, red);
        }
        else if (skin == "astro-bot")
        {
            FillRect(grid, cx - 5 + eyeDx, ey + 1 + eyeDy, 3, 3, cyan);
            FillRect(grid, cx + 2 + eyeDx, ey + 1 + eyeDy, 3, 3, cyan);
            SetPix(grid, cx - 5 + eyeDx, ey + 1 + eyeDy, white);
            SetPix(grid, cx + 2 + eyeDx, ey + 1 + eyeDy, white);
        }
        else if (skin == "shadow-cat")
        {
            FillRect(grid, cx - 6, ey, 4, 4, gold);
            FillRect(grid, cx + 2, ey, 4, 4, gold);
            FillRect(grid, cx - 5 + eyeDx, ey + 1 + eyeDy, 2, 2, outline);
            FillRect(grid, cx + 3 + eyeDx, ey + 1 + eyeDy, 2, 2, outline);
            SetPix(grid, cx - 6, ey, white);
            SetPix(grid, cx + 2, ey, white);
        }
        else
        {
            FillRect(grid, cx - 6, ey, 4, 4, eyeBg);
            FillRect(grid, cx + 2, ey, 4, 4, eyeBg);
            FillRect(grid, cx - 5 + eyeDx, ey + 1 + eyeDy, 3, 3, pupil);
            FillRect(grid, cx + 2 + eyeDx, ey + 1 + eyeDy, 3, 3, pupil);
            SetPix(grid, cx - 5, ey, white);
            SetPix(grid, cx + 2, ey, white);
        }

        if (waveArm)
        {
            int armLift = (frame % 2 == 0) ? -1 : -4;
            FillRect(grid, cx + 8, cy + armLift, 3, 4, cream);
        }

        AddOutline(grid, outline);

        if (typing)
        {
            Color lapColor = Color.FromArgb(255, 51, 65, 85);
            Color scrColor = (frame % 2 == 0) ? cyan : Color.FromArgb(255, 125, 211, 252);
            FillRect(grid, cx - 6, cy + 4, 12, 6, lapColor);
            FillRect(grid, cx - 5, cy + 5, 10, 4, scrColor);
            int pawX = (frame % 2 == 0) ? cx - 4 : cx + 2;
            FillRect(grid, pawX, cy + 9, 3, 2, cream);
        }
        if (pose == "failed")
        {
            DrawSymbol(grid, "X", 27, (frame % 2 == 0) ? 3 : 5, red);
        }
        else if (pose == "waiting")
        {
            DrawSymbol(grid, "?", 28, (frame % 2 == 0) ? 2 : 4, amber);
        }
        else if (pose == "review")
        {
            DrawSymbol(grid, "star", 3, (frame % 2 == 0) ? 2 : 4, gold);
            DrawSymbol(grid, "star", 28, (frame % 2 == 0) ? 5 : 2, green);
        }

        return grid;
    }

    private static void DrawToBmp(Bitmap bmp, Color[,] grid, int col, int row, bool flipX)
    {
        int ox = col * CellW + PadX;
        int oy = row * CellH + PadY;
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            var brushes = new Dictionary<int, SolidBrush>();
            for (int y = 0; y < GridH; y++)
            {
                for (int x = 0; x < GridW; x++)
                {
                    Color c = grid[x, y];
                    if (c.A == 0) continue;
                    int key = c.ToArgb();
                    SolidBrush b;
                    if (!brushes.TryGetValue(key, out b))
                    {
                        b = new SolidBrush(c);
                        brushes[key] = b;
                    }
                    int dx = flipX ? (GridW - 1 - x) : x;
                    g.FillRectangle(b, ox + dx * Px, oy + y * Px, Px, Px);
                }
            }
            foreach (var b in brushes.Values) b.Dispose();
        }
    }

    public static void GenerateAll(string outRoot)
    {
        Directory.CreateDirectory(outRoot);
        string[,] defs = {
            { "cyber-fox", "小蓝狐 CyberFox", "赛博蓝色小狐狸（默认）" },
            { "astro-bot", "像素宇航员 Astro", "带发光面罩的迷你太空人" },
            { "shadow-cat", "小黑猫 ShadowCat", "金色大眼的灵动黑猫" },
            { "byte-ghost", "电子幽灵 ByteGhost", "呆萌漂浮的霓虹小幽灵" },
            { "shiba-dog", "像素柴犬 Shiba", "暖黄卷尾的豆豆眉小柴犬" },
            { "pixel-dragon", "像素小青龙 Dragon", "长着金角小翅膀的翡翠青龙" }
        };

        for (int i = 0; i < defs.GetLength(0); i++)
        {
            string id = defs[i, 0];
            string name = defs[i, 1];
            string desc = defs[i, 2];
            string dir = Path.Combine(outRoot, id);
            Directory.CreateDirectory(dir);

            using (var bmp = new Bitmap(CellW * Cols, CellH * Rows, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Transparent);

                for (int c = 0; c < 6; c++) DrawToBmp(bmp, RenderFrame(id, "idle", c, -1), c, 0, false);
                for (int c = 0; c < 8; c++) DrawToBmp(bmp, RenderFrame(id, "run", c, -1), c, 1, false);
                for (int c = 0; c < 8; c++) DrawToBmp(bmp, RenderFrame(id, "run", c, -1), c, 2, true);
                for (int c = 0; c < 4; c++) DrawToBmp(bmp, RenderFrame(id, "wave", c, -1), c, 3, false);
                for (int c = 0; c < 5; c++) DrawToBmp(bmp, RenderFrame(id, "jump", c, -1), c, 4, false);
                for (int c = 0; c < 8; c++) DrawToBmp(bmp, RenderFrame(id, "failed", c, -1), c, 5, false);
                for (int c = 0; c < 6; c++) DrawToBmp(bmp, RenderFrame(id, "waiting", c, -1), c, 6, false);
                for (int c = 0; c < 6; c++) DrawToBmp(bmp, RenderFrame(id, "work", c, -1), c, 7, false);
                for (int c = 0; c < 6; c++) DrawToBmp(bmp, RenderFrame(id, "review", c, -1), c, 8, false);
                for (int c = 0; c < 8; c++) DrawToBmp(bmp, RenderFrame(id, "look", 0, c), c, 9, false);
                for (int c = 0; c < 8; c++) DrawToBmp(bmp, RenderFrame(id, "look", 0, c + 8), c, 10, false);

                bmp.Save(Path.Combine(dir, "spritesheet.png"), ImageFormat.Png);
            }

            string json = "{\n" +
                "  \"id\": \"" + id + "\",\n" +
                "  \"displayName\": \"" + name + "\",\n" +
                "  \"description\": \"" + desc + "\",\n" +
                "  \"spriteVersionNumber\": 2,\n" +
                "  \"spritesheetPath\": \"spritesheet.png\"\n" +
                "}\n";
            File.WriteAllText(Path.Combine(dir, "pet.json"), json, new UTF8Encoding(false));
        }
    }
}
"@

[CodexPetGenerator]::GenerateAll($OutRoot)
Write-Host "All 6 balanced (36x39) Codex v2 pets generated in $OutRoot"
