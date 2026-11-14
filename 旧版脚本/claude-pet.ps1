# Claude 小螃蟹桌宠 —— 纯 PowerShell + WinForms，无需安装任何东西
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$SZ    = 7     # 一个像素格的大小（改大就变大）
$COLS = 12
$ROWS = 12
$TOP  = 5     # 身体从第 5 行开始，上面留给爱心和 zzz

$P = @{
  state = 'fall'; dir = 1; vy = 0.0; x = 0.0; y = 0.0
  timer = 60; frame = 0; tick = 0; blink = 0; happy = 0; squash = 0
  drag = $false; dragMoved = $false; offX = 0; offY = 0; downX = 0; downY = 0
  hearts = New-Object System.Collections.ArrayList
}
$rand = New-Object System.Random

$orange = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(217, 119, 87))
$dark   = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(25, 25, 25))
$pink   = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(240, 90, 120))
$gray   = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(110, 110, 140))
$zfont  = New-Object Drawing.Font ('Consolas', 9, [Drawing.FontStyle]::Bold)
$key    = [Drawing.Color]::FromArgb(255, 0, 255)

$form = New-Object Windows.Forms.Form
$form.Text = 'Claude Pet'
$form.FormBorderStyle = 'None'
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.StartPosition = 'Manual'
$form.BackColor = $key
$form.TransparencyKey = $key
$form.ClientSize = [Drawing.Size]::new($COLS * $SZ, $ROWS * $SZ)
$form.GetType().GetProperty('DoubleBuffered', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($form, $true, $null)

$wa0 = [Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$P.x = [double]($wa0.Left + $rand.Next(100, [Math]::Max(101, $wa0.Width - 200)))
$P.y = [double]$wa0.Top
$form.Location = [Drawing.Point]::new([int]$P.x, [int]$P.y)

function Cell($g, $b, $c, $r, $w, $h) { $g.FillRectangle($b, $c * $SZ, $r * $SZ, $w * $SZ, $h * $SZ) }

function Add-Hearts($n) {
  for ($i = 0; $i -lt $n; $i++) {
    [void]$P.hearts.Add(@{ x = [double]($rand.Next(1, $COLS * $SZ - 16)); y = [double](($TOP - 1) * $SZ); life = 30 + $rand.Next(15) })
  }
}

function Pick {
  $r = $rand.Next(100)
  if ($r -lt 55) {
    $P.state = 'walk'; $P.dir = if ($rand.Next(2) -eq 0) { 1 } else { -1 }; $P.timer = $rand.Next(60, 220)
  } elseif ($r -lt 90) {
    $P.state = 'idle'; $P.timer = $rand.Next(40, 140)
  } else {
    $P.state = 'sleep'; $P.timer = $rand.Next(250, 600)
  }
}

function Poke {
  if ($P.state -eq 'sleep') {
    $P.state = 'idle'; $P.timer = 50; $P.happy = 25; Add-Hearts 2
  } elseif ($P.state -ne 'jump' -and $P.state -ne 'fall') {
    $P.state = 'jump'; $P.vy = -11.0; $P.happy = 30; Add-Hearts 3
  }
}

$form.Add_Paint({
  param($s, $e)
  $g = $e.Graphics
  $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::SingleBitPerPixelGridFit
  $sq = if ($P.squash -gt 0) { 1 } else { 0 }
  $b = $TOP + $sq
  $air = ($P.state -eq 'jump' -or $P.state -eq 'fall' -or $P.drag)

  # 腿
  $legs = 2, 4, 7, 9
  for ($i = 0; $i -lt 4; $i++) {
    $lh = 2 - $sq
    if ($P.state -eq 'walk' -and -not $air -and (($i % 2) -eq ($P.frame % 2))) { $lh = 1 }
    Cell $g $orange $legs[$i] ($b + 5) 1 $lh
  }
  # 身体
  Cell $g $orange 2 $b 8 5
  # 小手（被拎起来时举高）
  $ar = if ($P.drag) { $b + 1 } else { $b + 2 }
  Cell $g $orange 0 $ar 2 2
  Cell $g $orange 10 $ar 2 2

  # 眼睛
  $eyes = if ($P.state -eq 'walk') { if ($P.dir -gt 0) { 4, 8 } else { 3, 7 } } else { 3, 8 }
  $closed = ($P.state -eq 'sleep' -or $P.blink -gt 0 -or $P.happy -gt 0)
  foreach ($ex in $eyes) {
    if ($closed) {
      $g.FillRectangle($dark, $ex * $SZ, ($b + 2) * $SZ, $SZ, [int]($SZ / 2))
    } else {
      Cell $g $dark $ex ($b + 1) 1 2
    }
  }

  # zzz
  if ($P.state -eq 'sleep') {
    for ($i = 0; $i -lt 3; $i++) {
      if (($P.frame % 4) -ge $i) {
        $g.DrawString('z', $zfont, $gray, [single](8 * $SZ + $i * 6), [single](($TOP - 1 - $i) * $SZ))
      }
    }
  }

  # 爱心
  foreach ($h in $P.hearts) {
    $px = 3; $hx = [int]$h.x; $hy = [int]$h.y
    $g.FillRectangle($pink, $hx + $px, $hy, $px, $px)
    $g.FillRectangle($pink, $hx + 3 * $px, $hy, $px, $px)
    $g.FillRectangle($pink, $hx, $hy + $px, 5 * $px, $px)
    $g.FillRectangle($pink, $hx + $px, $hy + 2 * $px, 3 * $px, $px)
    $g.FillRectangle($pink, $hx + 2 * $px, $hy + 3 * $px, $px, $px)
  }
})

$timer = New-Object Windows.Forms.Timer
$timer.Interval = 33
$timer.Add_Tick({
  $P.tick++
  $center = [Drawing.Point]::new([int]($P.x + $form.Width / 2), [int]($P.y + $form.Height / 2))
  $wa = [Windows.Forms.Screen]::FromPoint($center).WorkingArea
  $ground = $wa.Bottom - $form.Height

  if (-not $P.drag) {
    if ($P.state -eq 'walk') {
      $P.x += $P.dir * 2
      if ($P.tick % 6 -eq 0) { $P.frame++ }
      if ($P.x -lt $wa.Left) { $P.x = $wa.Left; $P.dir = 1 }
      if ($P.x -gt $wa.Right - $form.Width) { $P.x = $wa.Right - $form.Width; $P.dir = -1 }
      $P.timer--; if ($P.timer -le 0) { Pick }
    } elseif ($P.state -eq 'idle') {
      $P.timer--; if ($P.timer -le 0) { Pick }
    } elseif ($P.state -eq 'sleep') {
      if ($P.tick % 15 -eq 0) { $P.frame++ }
      $P.timer--; if ($P.timer -le 0) { $P.state = 'idle'; $P.timer = 40 }
    } else {
      $P.vy += 1.0
      $P.y += $P.vy
      if ($P.y -ge $ground) {
        $P.y = $ground; $P.vy = 0.0; $P.squash = 5; $P.state = 'idle'; $P.timer = 30
      }
    }
    if ($P.state -ne 'jump' -and $P.state -ne 'fall') {
      if ($P.y -lt $ground - 1) { $P.state = 'fall'; $P.vy = 0.0 }
      elseif ($P.y -gt $ground) { $P.y = $ground }
    }
  }

  if ($P.squash -gt 0) { $P.squash-- }
  if ($P.happy -gt 0) { $P.happy-- }
  if ($P.blink -gt 0) { $P.blink-- } elseif ($rand.Next(110) -eq 0) { $P.blink = 4 }

  for ($i = $P.hearts.Count - 1; $i -ge 0; $i--) {
    $h = $P.hearts[$i]; $h.y -= 1.2; $h.life--
    if ($h.life -le 0 -or $h.y -lt 0) { $P.hearts.RemoveAt($i) }
  }

  $form.Location = [Drawing.Point]::new([int]$P.x, [int]$P.y)
  $form.Invalidate()
})

$form.Add_MouseDown({
  param($s, $e)
  if ($e.Button -eq [Windows.Forms.MouseButtons]::Left) {
    $c = [Windows.Forms.Cursor]::Position
    $P.drag = $true; $P.dragMoved = $false
    $P.offX = $c.X - $P.x; $P.offY = $c.Y - $P.y; $P.downX = $c.X; $P.downY = $c.Y
  }
})
$form.Add_MouseMove({
  if ($P.drag) {
    $c = [Windows.Forms.Cursor]::Position
    if ([Math]::Abs($c.X - $P.downX) + [Math]::Abs($c.Y - $P.downY) -gt 4) { $P.dragMoved = $true }
    if ($P.dragMoved) {
      $P.x = [double]($c.X - $P.offX); $P.y = [double]($c.Y - $P.offY)
      $form.Location = [Drawing.Point]::new([int]$P.x, [int]$P.y)
    }
  }
})
$form.Add_MouseUp({
  param($s, $e)
  if ($e.Button -eq [Windows.Forms.MouseButtons]::Left -and $P.drag) {
    $P.drag = $false
    if ($P.dragMoved) { $P.state = 'fall'; $P.vy = 0.0 } else { Poke }
  }
})

$menu = New-Object Windows.Forms.ContextMenuStrip
$menu.Items.Add('跳一下 ♥').Add_Click({ Poke })
$menu.Items.Add('去睡觉 / 叫醒').Add_Click({
  if ($P.state -eq 'sleep') { Poke } elseif ($P.state -ne 'jump' -and $P.state -ne 'fall') { $P.state = 'sleep'; $P.timer = 600 }
})
[void]$menu.Items.Add('-')
$menu.Items.Add('拜拜（退出）').Add_Click({ $timer.Stop(); $form.Close() })
$form.ContextMenuStrip = $menu

$timer.Start()
[System.Windows.Forms.Application]::Run($form)
