<img src="icon.png" alt="Terminal that can kill" width="84" align="left">

# <span style="color: #cc0000;">TERMINAL THAT CAN KILL</span>

![Lethal Company](https://img.shields.io/badge/Lethal%20Company-V81-cc0000?style=flat-square)
![BepInEx](https://img.shields.io/badge/BepInEx-5.4.21%2B-cc0000?style=flat-square)
![Version](https://img.shields.io/badge/version-1.0.9-cc0000?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-cc0000?style=flat-square)

<br clear="left">

**Language / Язык:** [English](#english) · [Русский](#russian)

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

<a name="english"></a>
## <span style="color: #cc0000;">TERMINAL THAT CAN KILL</span>

**Author:** <span style="color: #cc0000;">Solo00n</span>

The ship and facility doors slam shut on anyone in the doorway, and the terminal stops disabling traps — it detonates mines and drives turrets berserk instead.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">WHAT IT DOES</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Deadly ship door</strong> — the hangar door crushes any player/monster standing in the opening when it closes (on the surface only).
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Deadly facility doors</strong> — the big terminal-code security doors crush whoever is in the opening when they slam shut.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Remote mine detonation</strong> — entering a mine code <em>blows it up</em> instead of disabling it; a critical error chain-detonates nearby mines.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Turret rampage</strong> — entering a turret code sends it <em>berserk</em> (spins &amp; fires at everyone); a critical error extends the rampage.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Turret head flip</strong> — after leaving berserk a turret smoothly turns its head 180° and rests facing the opposite way.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Barber-style death</strong> — door kills reuse the Barber (Clay Surgeon) death: the body is launched up and ragdolls.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Fully configurable</strong> — every mechanic can be tuned or turned off.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">HOW IT WORKS</span>

* <span style="color: #cc0000;">▶</span> The kill zone is a thin oriented slab sitting <strong style="color: #cc0000;">in the doorway plane</strong>, not a proximity sphere — only someone standing in the opening is caught.
* <span style="color: #cc0000;">▶</span> Ship door: detected via <code>doorPower &lt; 1</code> + the <code>ShipDoorClose</code> animator state; the doorway is at a fixed world position (the ship never moves).
* <span style="color: #cc0000;">▶</span> Facility doors: detected on the <code>SetDoorOpen</code> open→closed transition and processed frame-by-frame.
* <span style="color: #cc0000;">▶</span> Traps use the vanilla synced RPCs (<code>ExplodeMineServerRpc</code>, <code>EnterBerserkModeServerRpc</code>) — no custom network objects.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">MULTIPLAYER (HOST-AUTHORITATIVE)</span>

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
Both the <strong style="color: #cc0000;">host and all clients must install the mod.</strong> It changes shared game rules, not one player's private state.
<br><br>
• <strong style="color: #cc0000;">Players:</strong> each client only kills its own local player; <code>KillPlayer</code> syncs that death to everyone, so exactly one authoritative kill happens.<br>
• <strong style="color: #cc0000;">Monsters:</strong> only the host iterates and kills enemies (the host owns enemy AI), which syncs to clients.<br>
• <strong style="color: #cc0000;">Traps:</strong> mine/turret effects fire through the game's own server RPCs, so all clients see the same result.
</blockquote>

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">REQUIREMENTS</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">BepInEx</strong> 5.4.21+ (<code>BepInEx-BepInExPack-5.4.2100</code>)
* <span style="color: #cc0000;">▶</span> Lethal Company <strong style="color: #cc0000;">V81</strong>

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">INSTALLATION</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Mod manager</strong> (r2modman / Thunderstore Mod Manager): search for the mod and click Install.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Manual:</strong> install the BepInEx pack, then drop <code>Solon.TerminalThatCanKill.dll</code> into <code>BepInEx/plugins/</code>.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">CONFIGURATION</span>

File: <code>BepInEx/config/Solon.TerminalThatCanKill.cfg</code> (created on first launch).

<table border="1" style="border-collapse: collapse; border: 1px solid #cc0000;">
<tr style="background: #1a1a1a;">
<th style="color: #cc0000;">Key</th><th style="color: #cc0000;">Default</th><th style="color: #cc0000;">Description</th>
</tr>
<tr><td><code>Enabled</code></td><td><code>true</code></td><td>Master switch for the deadly-doors mechanic.</td></tr>
<tr><td><code>KillPlayers</code></td><td><code>true</code></td><td>Kill players caught in a closing doorway.</td></tr>
<tr><td><code>KillMonsters</code></td><td><code>false</code></td><td>Kill ship-capable monsters caught in a closing doorway.</td></tr>
<tr><td><code>AffectedDoors</code></td><td><code>Both</code></td><td><code>ShipDoor</code> / <code>TerminalDoors</code> / <code>Both</code>.</td></tr>
<tr><td><code>SafeZoneSeconds</code></td><td><code>5</code></td><td>Grace period after landing during which doors do not kill.</td></tr>
<tr><td><code>DamageMode</code></td><td><code>InstantKill</code></td><td><code>InstantKill</code> or <code>DamageOverTime</code>.</td></tr>
<tr><td><code>EnableRemoteControl</code></td><td><code>true</code></td><td>Detonate/rampage traps instead of disabling them.</td></tr>
<tr><td><code>MineErrorChance</code></td><td><code>0.15</code></td><td>Chance a mine command misfires into a chain blast.</td></tr>
<tr><td><code>TurretErrorChance</code></td><td><code>0.15</code></td><td>Chance a turret command misfires into a sustained rampage.</td></tr>
<tr><td><code>TurretFlipOnBerserkExit</code></td><td><code>true</code></td><td>Turret head turns 180° after leaving berserk.</td></tr>
</table>

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
Advanced tuning (zone size <code>DoorwayThickness/Width/Height</code>, close timings, <code>PlayerDeathAnimationId</code>, chain radius, rampage duration) lives in the <code>Doors.Advanced</code> / <code>RemoteControl.Advanced</code> sections of the config.
</blockquote>

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">COMPATIBILITY</span>

* <span style="color: #cc0000;">▶</span> Works on vanilla and modded moons (<strong style="color: #cc0000;">LethalLevelLoader</strong>) — doors are found dynamically, no hard-coded scenes.
* <span style="color: #cc0000;">▶</span> No custom network objects; relies on vanilla RPCs — low conflict risk with other terminal mods.
* <span style="color: #cc0000;">▶</span> If another mod also patches the terminal code function, set <code>EnableRemoteControl = false</code> to avoid overlap.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">BUILD</span>

<pre style="border: 1px solid #cc0000; padding: 10px;">dotnet build LethalDoors.csproj -c Release</pre>

Output: <code>bin/Release/Solon.TerminalThatCanKill.dll</code>. Game assemblies are referenced via the <code>LethalCompany.GameLibs.Steam</code> NuGet package as compile-only (<code>PrivateAssets="all"</code>) — no game files are distributed.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">CREDITS</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Solo00n</strong> — author.
* <span style="color: #cc0000;">▶</span> Built on <strong style="color: #cc0000;">BepInEx</strong> &amp; <strong style="color: #cc0000;">HarmonyX</strong>.
* <span style="color: #cc0000;">▶</span> Inspiration: <em>Lethal Doors</em> by saint_kendrick (ship-door approach) and <em>RemoteMineDetonation</em> by jacksonb-cs (terminal detonation idea).
* <span style="color: #cc0000;">▶</span> Licensed under <strong style="color: #cc0000;">MIT</strong>.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

<a name="russian"></a>
## <span style="color: #cc0000;">TERMINAL THAT CAN KILL</span>

**Автор:** <span style="color: #cc0000;">Solo00n</span>

Двери корабля и комплекса захлопываются насмерть на том, кто стоит в проёме, а терминал больше не отключает ловушки — вместо этого он детонирует мины и сводит турели с ума.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">ЧТО ДЕЛАЕТ МОД</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Смертельная дверь корабля</strong> — рампа раздавливает игрока/монстра в проёме при закрытии (только на поверхности).
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Смертельные двери комплекса</strong> — большие двери с кодом раздавливают того, кто стоит в проёме при захлопывании.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Удалённая детонация мин</strong> — ввод кода мины <em>взрывает</em> её вместо отключения; при критической ошибке детонируют соседние мины.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Бешенство турели</strong> — ввод кода турели вводит её в <em>берсерк</em> (крутится и стреляет по всем); ошибка продлевает раж.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Разворот головы турели</strong> — после берсерка турель плавно поворачивает голову на 180° и остаётся смотреть в другую сторону.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Смерть как у Barber</strong> — смерть от двери повторяет смерть от Barber (Clay Surgeon): тело подбрасывает вверх.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Полная настройка</strong> — любую механику можно подстроить или выключить.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">КАК ЭТО РАБОТАЕТ</span>

* <span style="color: #cc0000;">▶</span> Зона поражения — тонкая ориентированная «плита» <strong style="color: #cc0000;">в плоскости проёма</strong>, а не сфера: убивает только того, кто стоит в проёме.
* <span style="color: #cc0000;">▶</span> Дверь корабля: определяется по <code>doorPower &lt; 1</code> + состоянию аниматора <code>ShipDoorClose</code>; проём — в фиксированной точке мира (корабль не двигается).
* <span style="color: #cc0000;">▶</span> Двери комплекса: ловятся на переходе <code>SetDoorOpen</code> открыта→закрыта и обрабатываются покадрово.
* <span style="color: #cc0000;">▶</span> Ловушки используют ванильные синхронизированные RPC (<code>ExplodeMineServerRpc</code>, <code>EnterBerserkModeServerRpc</code>) — без своих сетевых объектов.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">МУЛЬТИПЛЕЕР (HOST-AUTHORITATIVE)</span>

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
Мод должен стоять <strong style="color: #cc0000;">и у хоста, и у всех клиентов.</strong> Он меняет общие правила игры, а не приватное состояние одного игрока.
<br><br>
• <strong style="color: #cc0000;">Игроки:</strong> каждый клиент убивает только своего локального игрока; <code>KillPlayer</code> синхронизирует смерть на всех — ровно одно авторитетное убийство.<br>
• <strong style="color: #cc0000;">Монстры:</strong> перебирает и убивает врагов только хост (владеет ИИ), что синхронизируется клиентам.<br>
• <strong style="color: #cc0000;">Ловушки:</strong> эффекты мин/турелей идут через серверные RPC самой игры, поэтому итог одинаков у всех.
</blockquote>

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">ЗАВИСИМОСТИ</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">BepInEx</strong> 5.4.21+ (<code>BepInEx-BepInExPack-5.4.2100</code>)
* <span style="color: #cc0000;">▶</span> Lethal Company <strong style="color: #cc0000;">V81</strong>

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">УСТАНОВКА</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Через менеджер</strong> (r2modman / Thunderstore Mod Manager): найти мод и нажать Install.
* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Вручную:</strong> установить BepInEx-пак, затем положить <code>Solon.TerminalThatCanKill.dll</code> в <code>BepInEx/plugins/</code>.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">НАСТРОЙКА</span>

Файл: <code>BepInEx/config/Solon.TerminalThatCanKill.cfg</code> (создаётся при первом запуске).

<table border="1" style="border-collapse: collapse; border: 1px solid #cc0000;">
<tr style="background: #1a1a1a;">
<th style="color: #cc0000;">Ключ</th><th style="color: #cc0000;">По умолчанию</th><th style="color: #cc0000;">Описание</th>
</tr>
<tr><td><code>Enabled</code></td><td><code>true</code></td><td>Главный выключатель механики смертельных дверей.</td></tr>
<tr><td><code>KillPlayers</code></td><td><code>true</code></td><td>Убивать игроков в закрывающемся проёме.</td></tr>
<tr><td><code>KillMonsters</code></td><td><code>false</code></td><td>Убивать монстров (способных войти в корабль) в проёме.</td></tr>
<tr><td><code>AffectedDoors</code></td><td><code>Both</code></td><td><code>ShipDoor</code> / <code>TerminalDoors</code> / <code>Both</code>.</td></tr>
<tr><td><code>SafeZoneSeconds</code></td><td><code>5</code></td><td>Безопасный период после посадки, пока двери не убивают.</td></tr>
<tr><td><code>DamageMode</code></td><td><code>InstantKill</code></td><td><code>InstantKill</code> или <code>DamageOverTime</code>.</td></tr>
<tr><td><code>EnableRemoteControl</code></td><td><code>true</code></td><td>Детонация/раж ловушек вместо отключения.</td></tr>
<tr><td><code>MineErrorChance</code></td><td><code>0.15</code></td><td>Шанс сбоя команды мины (цепной взрыв).</td></tr>
<tr><td><code>TurretErrorChance</code></td><td><code>0.15</code></td><td>Шанс сбоя команды турели (продлённый раж).</td></tr>
<tr><td><code>TurretFlipOnBerserkExit</code></td><td><code>true</code></td><td>Разворот головы турели на 180° после берсерка.</td></tr>
</table>

<blockquote style="border-left: 4px solid #cc0000; padding-left: 15px;">
Тонкая настройка (размеры зоны <code>DoorwayThickness/Width/Height</code>, тайминги закрытия, <code>PlayerDeathAnimationId</code>, радиус цепи, длительность ража) — в разделах <code>Doors.Advanced</code> / <code>RemoteControl.Advanced</code> конфига.
</blockquote>

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">СОВМЕСТИМОСТЬ</span>

* <span style="color: #cc0000;">▶</span> Работает на ванильных и модовых лунах (<strong style="color: #cc0000;">LethalLevelLoader</strong>) — двери находятся динамически, без хардкода сцен.
* <span style="color: #cc0000;">▶</span> Не создаёт своих сетевых объектов, опирается на ванильные RPC — низкий риск конфликтов.
* <span style="color: #cc0000;">▶</span> Если другой мод тоже патчит функцию кода терминала — поставьте <code>EnableRemoteControl = false</code>.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">СБОРКА</span>

<pre style="border: 1px solid #cc0000; padding: 10px;">dotnet build LethalDoors.csproj -c Release</pre>

Результат: <code>bin/Release/Solon.TerminalThatCanKill.dll</code>. Сборки игры подключены через NuGet-пакет <code>LethalCompany.GameLibs.Steam</code> только для компиляции (<code>PrivateAssets="all"</code>) — файлы игры не распространяются.

<hr style="border: 1px solid #cc0000; margin: 15px 0;">

### <span style="color: #cc0000;">БЛАГОДАРНОСТИ</span>

* <span style="color: #cc0000;">▶</span> <strong style="color: #cc0000;">Solo00n</strong> — автор.
* <span style="color: #cc0000;">▶</span> Построено на <strong style="color: #cc0000;">BepInEx</strong> и <strong style="color: #cc0000;">HarmonyX</strong>.
* <span style="color: #cc0000;">▶</span> Идеи: <em>Lethal Doors</em> от saint_kendrick (подход к двери корабля) и <em>RemoteMineDetonation</em> от jacksonb-cs (детонация из терминала).
* <span style="color: #cc0000;">▶</span> Лицензия <strong style="color: #cc0000;">MIT</strong>.
