using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using RTLC.FallenAces;

var catalog = new TranslationCatalog();
catalog.Load(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../translations")));
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
Check(catalog.Count > 2900, "Complete catalogs must be loaded");
Check(catalog.Translate("Picked up Gold Pocket Watch") == "\u041f\u043e\u0434\u043e\u0431\u0440\u0430\u043d\u043e: \u0417\u043e\u043b\u043e\u0442\u044b\u0435 \u043a\u0430\u0440\u043c\u0430\u043d\u043d\u044b\u0435 \u0447\u0430\u0441\u044b", "World name and journal heading: Picked up Gold Pocket Watch");
Check(catalog.Translate("Picked up Safety Deposit Box Key") == "\u041f\u043e\u0434\u043e\u0431\u0440\u0430\u043d\u043e: \u041a\u043b\u044e\u0447 \u043e\u0442 \u0431\u0430\u043d\u043a\u043e\u0432\u0441\u043a\u043e\u0439 \u044f\u0447\u0435\u0439\u043a\u0438", "World name and journal heading: Picked up Safety Deposit Box Key");
Check(catalog.Translate("Doctor's Notes") == "\u0417\u0430\u043f\u0438\u0441\u043a\u0430 \u0432\u0440\u0430\u0447\u0430", "World name and journal heading: Doctor's Notes");
Check(catalog.Translate("Barbershop Owner's Notes") == "\u0417\u0430\u043c\u0435\u0442\u043a\u0438 \u0432\u043b\u0430\u0434\u0435\u043b\u044c\u0446\u0430 \u043f\u0430\u0440\u0438\u043a\u043c\u0430\u0445\u0435\u0440\u0441\u043a\u043e\u0439", "World name and journal heading: Barbershop Owner's Notes");
Check(catalog.Translate("Meredith Car Key") == "\u041a\u043b\u044e\u0447 \u043e\u0442 \u043c\u0430\u0448\u0438\u043d\u044b \u00ab\u041c\u0435\u0440\u0435\u0434\u0438\u0442\u00bb", "World name and journal heading: Meredith Car Key");

Check(catalog.Translate("WATER UNDER THE BRIDGE") == "\u0427\u0422\u041e \u0411\u042b\u041b\u041e, \u0422\u041e \u041f\u0420\u041e\u0428\u041b\u041e", "Gameplay localization: WATER UNDER THE BRIDGE");
Check(catalog.Translate("SHIFTING GEARS") == "\u0421\u041c\u0415\u041d\u0410 \u041f\u0415\u0420\u0415\u0414\u0410\u0427", "Gameplay localization: SHIFTING GEARS");
Check(catalog.Translate("[Mash Q]") == "[\u0427\u0430\u0441\u0442\u043e \u043d\u0430\u0436\u0438\u043c\u0430\u0439\u0442\u0435 Q]", "Gameplay localization: [Mash Q]");
Check(catalog.Translate("E [Hold]") == "E (\u0423\u0434\u0435\u0440\u0436\u0438\u0432\u0430\u0442\u044c)", "Scenario regression: E [Hold]");
Check(catalog.Translate("Picked up Loot! + $2") == "\u041f\u043e\u0434\u043e\u0431\u0440\u0430\u043d\u043e \u0434\u0435\u043d\u0435\u0433: + $2", "Gameplay localization: Picked up Loot! + $2");
Check(catalog.Translate("No Target!") == "\u041d\u0435\u0442 \u0446\u0435\u043b\u0438!", "Gameplay localization: No Target!");
Check(catalog.Translate("Weapon is broken!") == "\u041e\u0440\u0443\u0436\u0438\u0435 \u0441\u043b\u043e\u043c\u0430\u043d\u043e!", "Gameplay localization: Weapon is broken!");
Check(catalog.Translate("Wallet") == "\u0411\u0443\u043c\u0430\u0436\u043d\u0438\u043a", "Gameplay localization: Wallet");
Check(catalog.Translate("Quarter") == "\u041c\u043e\u043d\u0435\u0442\u0430", "Coin name must not imply a denomination inconsistent with loot value");

Check(catalog.Translate("Main Menu") == "Главное меню", "Static menu translation");
Check(catalog.Translate("\n Main Menu \n") == "\n Главное меню \n", "Whitespace preservation");
Check(catalog.Translate("<color=red>Main Menu</color>") == "<color=red>Главное меню</color>", "Rich text tags must remain intact");
Check(catalog.Translate("Finished!") == "Завершено!", "Loading completion label");
Check(catalog.Translate("Getting Gianni...") == "Зовём Джанни...", "Loading joke label");
foreach (var stage in new[] { "Loading tommy guns...", "Hanging fedoras...", "Whacking wise guys...", "Getting Gianni...", "Making unrefusable offers...", "Fuhgetting about it...", "Agreeing with boss...", "Distributing cannoli..." })
    foreach (var suffix in new[] { "", " (This may take a while)" })
        Check(catalog.Translate(stage + suffix) != stage + suffix && !catalog.Translate(stage + suffix).Contains("This may take a while"), "Complete loading-message coverage: " + stage + suffix);

Check(catalog.Translate("Getting Gianni... (This may take a while)") == "Зовём Джанни... (это может занять время)", "Full loading joke with wait suffix");
Check(catalog.Translate("Hidden") == "Скрыт", "Fully hidden state");
Check(catalog.Translate("Partly Hidden") == "Частично скрыт", "Stealth labels refer to the male protagonist");
Check(catalog.Translate("Barbershop Storage Key") == "Ключ от кладовки парикмахерской", "Barbershop storage key name");
foreach (var spelling in new[] { "Stack of coins", "Stack Of Coins", "Stack of Coins" })
    Check(catalog.Translate(spelling) == "Стопка монет", "Coin stack spelling: " + spelling);
Check(catalog.Translate("Glasshearts") == "Стеклянные сердца", "Consistent gang name");
Check(catalog.Translate("Glassjaw") == "Стеклянная челюсть", "Consistent leader name");
Check(catalog.Translate("Glassjaw just got pinched!") == "Стеклянную челюсть только что повязали!", "Accusative case for leader nickname");
Check(catalog.Translate("Crystal.") == "\u041a\u0440\u0438\u0441\u0442\u0430\u043b\u044c\u043d\u043e \u044f\u0441\u043d\u043e.", "Scenario regression: Crystal.");
foreach (var file in Directory.GetFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../translations")), "*.json"))
    foreach (var pair in JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(file)))
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(pair.Key, @"glass\s*hearts?", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            Check(pair.Value.Contains("Стеклянн") && pair.Value.Contains("серд") && !pair.Value.Contains("Хруст"), "Gang terminology throughout catalogs: " + pair.Key);
        if (System.Text.RegularExpressions.Regex.IsMatch(pair.Key, @"Glass(?:jaw|aw)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            Check(pair.Value.Contains("Стеклянн") && pair.Value.Contains("челюст", StringComparison.OrdinalIgnoreCase) && !pair.Value.Contains("Хруст"), "Leader terminology throughout catalogs: " + pair.Key);
    }

Check(catalog.Translate("Apartment 204 Key") == "Ключ от квартиры 204", "Scripted apartment key name");
Check(catalog.Translate("Moved money.") == "Деньги перенесены.", "Note title independent of its colored body");
Check(catalog.Translate("Map of the streets") == "Карта улиц", "Scripted map title");

Check(catalog.Translate("Space") == "Пробел", "Keyboard key display");
Check(catalog.Translate("(Unstorable)") == "(\u041d\u0435\u043b\u044c\u0437\u044f \u0445\u0440\u0430\u043d\u0438\u0442\u044c)", "Parenthesized pickup status");
Check(catalog.Translate("<color=red>(Unconscious)</color>") == "<color=red>(Без сознания)</color>", "Tagged parenthesized status");
Check(catalog.Translate("Fridge Door") == "Дверца холодильника", "Dynamic object name");
Check(catalog.Translate("Turn off") == "Выключить", "Dynamic interaction verb");
Check(catalog.Translate("Wad Of Dough") == "Пачка денег", "Context-sensitive item name");
Check(catalog.Translate("DRRRINNG! - DRRRINNG!") == "ДЗЗЗЗЗЗЫНЬ! — ДЗЗЗЗЗЗЫНЬ!", "Dynamic comic sound effect");
Check(catalog.Translate("2x MSAA") == "MSAA ×2", "Antialiasing option");
Check(catalog.Translate("4x MSAA") == "MSAA ×4", "Antialiasing option");
Check(catalog.Translate("8x MSAA") == "MSAA ×8", "Antialiasing option");
Check(catalog.Translate("Mouse Sensitivity") == "Чувствительность", "Compact settings label");
Check(catalog.Translate("Screenshake") == "Тряска камеры", "User-edited settings label");
Check(catalog.Translate("Controller Input\n") == "Геймпад\n", "User-edited compact gamepad label");
Check(catalog.Translate("Gameplay") == "Игра", "Compact settings category");
Check(catalog.Translate("KEYBOARD & MOUSE") == "КЛАВИШИ И МЫШЬ", "Compact settings title");
Check(catalog.Translate("FPS: 120") == "Кадров/с: 120", "Dynamic FPS counter");
Check(catalog.Translate("Picked up Wad Of Dough") == "Подобрано: Пачка денег", "Dynamic pickup notification");
const string saveTimestamp = "11/02/2026 03:48:23";
Check(catalog.Translate("(Auto) Heart of Glass\nWater Under The Bridge\nNormal | " + saveTimestamp) ==
    "(Авто) Стеклянное сердце\nЧто было, то прошло\nОбычная | " + saveTimestamp,
    "Translate the generated autosave episode, chapter and difficulty while preserving the timestamp");
Check(catalog.Translate("(Quick) Heart of Glass\nWake Up Call\nHard | " + saveTimestamp) ==
    "(Быстрое) Стеклянное сердце\nТревожный звонок\nСложная | " + saveTimestamp,
    "Translate the generated quicksave prefix and its multiline details");
foreach (var difficulty in new[] { ("Easy", "Лёгкая"), ("Normal", "Обычная"), ("Hard", "Сложная"), ("Custom", "Своя") })
    Check(catalog.Translate(difficulty.Item1 + " | " + saveTimestamp) == difficulty.Item2 + " | " + saveTimestamp,
        "Translate save difficulty " + difficulty.Item1 + " without changing date/time");
Check(catalog.Translate("(Auto) Unrecognized Episode\nUnrecognized Chapter\nCustom | " + saveTimestamp) ==
    "(Авто) Unrecognized Episode\nUnrecognized Chapter\nСвоя | " + saveTimestamp,
    "Preserve unknown episode and chapter names inside save templates");
const string outdatedSave = "Wake Up Call\n<color=red>Outdated Save\nLevel Restart Required</color>";
const string translatedOutdatedSave = "Тревожный звонок\n<color=red>Устаревшее сохранение\nТребуется перезапуск уровня</color>";
Check(catalog.Translate(outdatedSave) == translatedOutdatedSave,
    "Translate multiline save warnings with tags that span multiple lines");
Check(catalog.Translate(translatedOutdatedSave) == translatedOutdatedSave,
    "Multiline rich-text translation must be idempotent");
Check(catalog.Translate("untouched_technical_identifier") == "untouched_technical_identifier", "Unknown identifiers must remain intact");
foreach (string file in Directory.GetFiles(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../translations")), "*.json"))
foreach (var pair in JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(file)))
    Check(catalog.Translate(pair.Value) == pair.Value, "Translation must be idempotent: " + pair.Key);

Check(catalog.Translate("Search for case critical clues (0/5)") == "\u041d\u0430\u0439\u0442\u0438 \u043a\u043b\u044e\u0447\u0435\u0432\u044b\u0435 \u0443\u043b\u0438\u043a\u0438 \u043f\u043e \u0434\u0435\u043b\u0443 (0/5)", "Scenario regression: Search for case critical clues (0/5)");
Check(catalog.Translate("Search for relevant clues (7/10)") == "\u041d\u0430\u0439\u0442\u0438 \u043f\u043e\u043b\u0435\u0437\u043d\u044b\u0435 \u0443\u043b\u0438\u043a\u0438 (7/10)", "Scenario regression: Search for relevant clues (7/10)");
Check(catalog.Translate("HOUSE OF CARDS") == "\u041a\u0410\u0420\u0422\u041e\u0427\u041d\u042b\u0419 \u0414\u041e\u041c\u0418\u041a", "Scenario regression: HOUSE OF CARDS");
Check(catalog.Translate("MOTH AND FLAME") == "\u041c\u041e\u0422\u042b\u041b\u0401\u041a \u0418 \u041f\u041b\u0410\u041c\u042f", "Scenario regression: MOTH AND FLAME");
Check(catalog.Translate("THE GETAWAY") == "\u041f\u041e\u0411\u0415\u0413", "Scenario regression: THE GETAWAY");
Check(catalog.Translate("KEY ITEMS") == "\u0412\u0410\u0416\u041d\u041e\u0415", "Scenario regression: KEY ITEMS");
Check(catalog.Translate("Map of Waltz's place") == "\u041a\u0430\u0440\u0442\u0430 \u0434\u043e\u043c\u0430 \u0423\u043e\u043b\u0442\u0446\u0430", "Scenario regression: Map of Waltz's place");

var dllCasesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/assembly_text_cases.json"));
var dllCases = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(dllCasesPath));
foreach (var pair in dllCases)
    Check(catalog.Translate(pair.Key) == pair.Value, "DLL literal regression: " + pair.Key);
if (args.Length > 0)
{
    var inventory = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(args[0]));
    var literals = new System.Collections.Generic.HashSet<string>();
    foreach (var assembly in inventory)
        foreach (var row in assembly["rows"]) literals.Add((string)row["text"]);
    foreach (var assembly in inventory)
        foreach (var row in assembly["rows"])
            if ((string)row["type"] == ".InputMaster" && ((string)row["text"]).TrimStart().StartsWith("{"))
            {
                var input = Newtonsoft.Json.Linq.JObject.Parse((string)row["text"]);
                foreach (var map in input["maps"])
                    if ((string)map["name"] == "Player")
                        foreach (var action in map["actions"])
                        {
                            var actionName = (string)action["name"];
                            // An unused developer test action, not a menu label.
                            if (actionName != "Test Move")
                                Check(catalog.Translate(actionName) != actionName, "Embedded player action name: " + actionName);
                        }
            }
    foreach (var pair in dllCases)
        Check(literals.Contains(pair.Key), "Reviewed DLL literal disappeared: " + pair.Key);
}
Console.WriteLine("PASS: " + dllCases.Count + " catalogued DLL literals and their exact rendered translations.");
var composedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/assembly_composed_cases.json"));
var composedCases = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(composedPath));
foreach (var pair in composedCases)
    Check(catalog.Translate(pair.Key) == pair.Value, "Assembled DLL text regression: " + pair.Key + " => " + catalog.Translate(pair.Key));
Console.WriteLine("PASS: " + composedCases.Count + " assembled DLL messages, status tags, counters and save captions.");
var serializedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/serialized_name_cases.json"));
var serializedCases = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(serializedPath));
foreach (var pair in serializedCases.Properties())
    Check(catalog.Translate(pair.Name) == (string)pair.Value["expected"], "Serialized display-name regression: " + pair.Name);
Console.WriteLine("PASS: " + serializedCases.Count + " reviewed serialized enemy/door/interaction names.");
var namingPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../tests/naming_context_cases.json"));
var namingCases = JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string,string>>(File.ReadAllText(namingPath));
foreach (var pair in namingCases)
    Check(catalog.Translate(pair.Key) == pair.Value, "Naming/context regression: " + pair.Key);

var temp = Path.Combine(Path.GetTempPath(), "rtlc-catalog-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    File.WriteAllText(Path.Combine(temp, "test.json"), "{\"Bought {0} - ${1}\":\"Куплено: {0} — ${1}\",\"Apple\":\"Яблоко\",\"Don’t\":\"Не надо\"}");
    catalog.Load(temp);
    Check(catalog.Translate("Bought Apple - $5") == "Куплено: Яблоко — $5", "Translate captured item names and preserve dollars");
    Check(catalog.Translate("Don\uFFFDt") == "Не надо", "Match legacy text read by the game as invalid UTF-8");
}
finally { File.Delete(Path.Combine(temp, "test.json")); Directory.Delete(temp); }
Console.WriteLine("PASS: catalogs, idempotency, tags, whitespace, dynamic item names, money and legacy encoding.");
