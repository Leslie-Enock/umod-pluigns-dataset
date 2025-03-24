namespace Oxide.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using System.Collections;
    using System.Text.RegularExpressions;
    using Oxide.Core;
    using Oxide.Core.Configuration;
    using Oxide.Core.Plugins;
    using UnityEngine;
    using Newtonsoft.Json.Linq;
    using Network;
    using System.ComponentModel;
    using Newtonsoft.Json.Converters;
    using UnityEngine.UI;

    [Info("Cui Generator", "bazuka5801", "3.1.60"), Description("Helps developer manage GUI manipulations in single line of code")]
    public class CuiGenerator : RustPlugin
    {
        public class CuiElementComparer : IEqualityComparer<CuiElement>
        {
            bool IEqualityComparer<CuiElement>.Equals(CuiElement x, CuiElement y) => EqualElement(x, y);

            int IEqualityComparer<CuiElement>.GetHashCode(CuiElement obj) => 0;

            public static bool EqualElement(CuiElement e1, CuiElement e2)
            {
                if (e1.Name != e2.Name)
                {
                    return false;
                }

                if (e1.Parent != e2.Parent)
                {
                    return false;
                }

                if (Math.Abs(e1.FadeOut - e2.FadeOut) > 0.01)
                {
                    return false;
                }

                if (e1.Components.Count != e2.Components.Count)
                {
                    return false;
                }

                return !e1.Components.Where((t, i) => !EqualComponent(t, e2.Components[i])).Any();
            }

            private static bool EqualComponent(ICuiComponent e1, ICuiComponent e2)
            {
                if (e1.Type != e2.Type)
                {
                    return false;
                }

                switch (e1.Type)
                {
                    case "RectTransform":
                        return EqualComponent((CuiRectTransformComponent)e1, (CuiRectTransformComponent)e2);
                    case "CountDown":
                        return EqualComponent((CuiCountdownComponent)e1, (CuiCountdownComponent)e2);
                    case "UnityEngine.UI.RawImage":
                        return EqualComponent((CuiRawImageComponent)e1, (CuiRawImageComponent)e2);
                    case "UnityEngine.UI.Text":
                        return EqualComponent((CuiTextComponent)e1, (CuiTextComponent)e2);
                    case "UnityEngine.UI.Image":
                        return EqualComponent((CuiImageComponent)e1, (CuiImageComponent)e2);
                    case "UnityEngine.UI.Button":
                        return EqualComponent((CuiButtonComponent)e1, (CuiButtonComponent)e2);
                    case "UnityEngine.UI.Outline":
                        return EqualComponent((CuiOutlineComponent)e1, (CuiOutlineComponent)e2);
                    case "UnityEngine.UI.InputField":
                        return EqualComponent((CuiInputFieldComponent)e1, (CuiInputFieldComponent)e2);
                }

                return false;
            }

            private static bool EqualComponent(CuiCountdownComponent e1, CuiCountdownComponent e2)
            {
                if (e1.Step != e2.Step)
                {
                    return false;
                }

                if (e1.StartTime != e2.StartTime)
                {
                    return false;
                }

                if (e1.EndTime != e2.EndTime)
                {
                    return false;
                }

                return e1.Command == e2.Command;
            }

            private static bool EqualComponent(CuiRectTransformComponent e1, CuiRectTransformComponent e2)
            {
                if (e1.AnchorMin != e2.AnchorMin)
                {
                    return false;
                }

                if (e1.AnchorMax != e2.AnchorMax)
                {
                    return false;
                }

                if (e1.OffsetMin != e2.OffsetMin)
                {
                    return false;
                }

                return e1.OffsetMax == e2.OffsetMax;
            }

            private static bool EqualComponent(CuiTextComponent e1, CuiTextComponent e2)
            {
                if (e1.Align != e2.Align)
                {
                    return false;
                }

                if (e1.Color != e2.Color)
                {
                    return false;
                }

                if (e1.Font != e2.Font)
                {
                    return false;
                }

                if (e1.Text != e2.Text)
                {
                    return false;
                }

                return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
            }

            private static bool EqualComponent(CuiButtonComponent e1, CuiButtonComponent e2)
            {
                if (e1.Command != e2.Command)
                {
                    return false;
                }

                if (e1.Close != e2.Close)
                {
                    return false;
                }

                if (e1.Color != e2.Color)
                {
                    return false;
                }

                if (e1.Sprite != e2.Sprite)
                {
                    return false;
                }

                if (e1.Material != e2.Material)
                {
                    return false;
                }

                if (e1.ImageType != e2.ImageType)
                {
                    return false;
                }

                return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
            }

            private static bool EqualComponent(CuiRawImageComponent e1, CuiRawImageComponent e2)
            {
                if (e1.Sprite != e2.Sprite)
                {
                    return false;
                }

                if (e1.Color != e2.Color)
                {
                    return false;
                }

                if (e1.Material != e2.Material)
                {
                    return false;
                }

                if (e1.Png != e2.Png)
                {
                    return false;
                }

                if (e1.Url != e2.Url)
                {
                    return false;
                }

                return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
            }

            private static bool EqualComponent(CuiImageComponent e1, CuiImageComponent e2)
            {
                if (e1.Sprite != e2.Sprite)
                {
                    return false;
                }

                if (e1.Color != e2.Color)
                {
                    return false;
                }

                if (e1.Png != e2.Png)
                {
                    return false;
                }

                if (e1.Material != e2.Material)
                {
                    return false;
                }

                if (e1.ImageType != e2.ImageType)
                {
                    return false;
                }

                return !(Math.Abs(e1.FadeIn - e2.FadeIn) > 0.01);
            }

            private static bool EqualComponent(CuiOutlineComponent e1, CuiOutlineComponent e2)
            {
                if (e1.Color != e2.Color)
                {
                    return false;
                }

                if (e1.Distance != e2.Distance)
                {
                    return false;
                }

                return e1.UseGraphicAlpha == e2.UseGraphicAlpha;
            }

            private static bool EqualComponent(CuiInputFieldComponent e1, CuiInputFieldComponent e2)
            {
                if (e1.Text != e2.Text)
                {
                    return false;
                }

                if (e1.Command != e2.Command)
                {
                    return false;
                }

                if (e1.Font != e2.Font)
                {
                    return false;
                }

                if (e1.FontSize != e2.FontSize)
                {
                    return false;
                }

                if (e1.CharsLimit != e2.CharsLimit)
                {
                    return false;
                }

                if (e1.Align != e2.Align)
                {
                    return false;
                }

                if (e1.IsPassword != e2.IsPassword)
                {
                    return false;
                }

                return true;
            }
        }
        [Serializable]
        internal class CuiFunction
        {
            public readonly List<List<CuiElement>> cacheArgs = new List<List<CuiElement>>();

            private int _argc = -1;
            public string GUI = "";

            public int argc()
            {
                if (_argc >= 0)
                {
                    return _argc;
                }

                _argc = 0;
                while (GUI.Contains("{" + _argc + "}"))
                {
                    _argc++;
                }

                return _argc;
            }
        }

        internal class CuiFunctionConverter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var data = (CuiFunction)value;
                writer.WriteValue(data.GUI);
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
                                            JsonSerializer serializer) =>
                new CuiFunction { GUI = reader.Value.ToString() };

            public override bool CanConvert(Type objectType) => objectType == typeof(CuiFunction);
        }

        private readonly HashSet<ulong> connected = new HashSet<ulong>();

        private readonly Dictionary<ulong, List<string>> uiCache = new Dictionary<ulong, List<string>>();

        private void OnServerInitialized()
        {
            CommunityEntity.ServerInstance.StartCoroutine(LoadFunctions());
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }

            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                OnPlayerConnected(player);
            }

            timer.Every(1f, () =>
            {
                if (isDebug)
                {
                    Puts($"CuiDraw: {draw}\nCuiDrawCache: {drawCache}");
                    draw = drawCache = 0;
                }
            });
        }

        private void Unload()
        {
            foreach (var player in players)
            {
                DestroyAllUI(player.Key);
            }
        }

        private void CacheUI(ulong userId, string json)
        {
            if (!uiCache.ContainsKey(userId))
            {
                uiCache[userId] = new List<string>();
            }

            var list = uiCache[userId];
            list.Add(json);
            if (list.Count > 5)
            {
                list.RemoveAt(0);
            }
        }

        private void PutsCacheUI(ulong userId)
        {
            if (!uiCache.ContainsKey(userId))
            {
                return;
            }

            var msg = string.Join("\n", uiCache[userId].ToArray());
            PrintError("AddUi error:\n" + msg);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (connected.Contains(player.userID))
            {
                connected.Remove(player.userID);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            connected.Add(player.userID);
            player.displayName = CleanName(player.displayName);
        }

        private static string CleanName(string strIn)
        {
            // Replace invalid characters with empty strings.
            try
            {
                return Regex.Replace(strIn, @"\$|\@|\\|\/", "",
                    RegexOptions.None);
            }
            // If we timeout when replacing invalid characters,
            // we should return Empty.
            catch
            {
                return strIn;
            }
        }

        private void OnPlayerAddUiDisconnected(ulong userId, string reason)
        {
            if (reason.ToLower().Contains("addui"))
            {
                PutsCacheUI(userId);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!players.ContainsKey(player))
            {
                return;
            }

            players.Remove(player);
        }

        //[HookMethod("OnPluginLoaded")]
        //void OnPluginLoaded(Plugin plugin)
        //{
        //    if (!string.IsNullOrEmpty(Net.sv.ip))
        //    {
        //        NextTick(()=>plugin.CallHook( "OnServerInitialized" ));
        //    }
        //}

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin.Name == Name)
            {
                return;
            }

            foreach (var playerPair in players)
            {
                for (var i = playerPair.Value.Count - 1; i >= 0; i--)
                {
                    var data = playerPair.Value[i];
                    if (data.plugin == plugin.Name)
                    {
                        IDestroyUI(playerPair.Key, data);
                    }
                }
            }
        }

        #region EXTERNAL CALLS

        internal void onCuiGeneratorInitialized()
        {
            isLoaded = true;
            Puts($"{nameof(CuiGenerator)} is ready!");
            Interface.CallHook("OnCuiGeneratorInitialized");
        }

        #endregion

        #region Fields

        private Dictionary<string, Dictionary<string, CuiFunction>> functions;
        private readonly Dictionary<BasePlayer, List<UIData>> players = new Dictionary<BasePlayer, List<UIData>>();

        private readonly CuiElementComparer CuiComparer = new CuiElementComparer();

        private bool isLoaded;

        private bool isDebug;
        private int drawCache;
        private int draw;

        #endregion

        #region Data

        private readonly DynamicConfigFile uiDB = Interface.Oxide.DataFileSystem.GetFile("CuiGenerator/CuiDB");
        private readonly DynamicConfigFile imagesDB = Interface.Oxide.DataFileSystem.GetFile("CuiGenerator/Images");

        #endregion

        #region UI

        private void IDrawUI(BasePlayer player, UIData data)
        {
            var now = DateTime.Now;
            if (!players.ContainsKey(player))
            {
                players.Add(player, new List<UIData>());
            }

            var funcs = players[player];
            for (var i = funcs.Count - 1; i >= 0; i--)
            {
                var func = funcs[i];
                if (func.plugin != data.plugin || func.funcName != data.funcName)
                {
                    continue;
                }

                players[player][i] = data;
                DrawUIWithoutCache(player, func, data);
                return;
            }

            var json = functions[data.plugin][data.funcName].GUI;
            if (data.args.Length > 0)
            {
                json = HandleArgs(json, data.args);
            }

            players[player].Add(data);
            if (data.additionalContainer.Count > 0)
            {
                var elements = CuiHelper.FromJson(json);
                elements.AddRange(data.additionalContainer);
                json = CuiHelper.ToJson(elements);
            }

            CacheUI(player.userID, json);
            CuiHelper.AddUi(player, json);
            if (isDebug)
            {
                draw++;
                Puts(json);
            }
        }

        private void DrawUIWithoutCache(BasePlayer player, UIData dataOld, UIData dataNew)
        {
            if (isDebug)
            {
                Puts($"AdditionalOldList => {string.Join(", ", dataOld.additionalContainer.Select(p => p.Name))}");
                Puts($"AdditionalNewList => {string.Join(", ", dataNew.additionalContainer.Select(p => p.Name))}");
            }

            var func = functions[dataOld.plugin][dataOld.funcName];

            var changedArgs = new List<int>();
            for (var i = 0; i < func.argc(); i++)
            {
                if (dataOld.args[i].ToString() != dataNew.args[i].ToString())
                {
                    changedArgs.Add(i);
                }
            }

            var destroylist = new List<CuiElement>();
            foreach (var arg in changedArgs)
            {
                destroylist.AddRange(func.cacheArgs[arg]);
            }

            // Additional container
            var additionalDestroyList =
                dataOld.additionalContainer.Except(dataNew.additionalContainer, CuiComparer).ToList();
            destroylist.AddRange(additionalDestroyList);
            if (isDebug)
            {
                Puts($"AdditionalDestroyList => {string.Join(", ", additionalDestroyList.Select(p => p.Name))}");
            }

            var additionalReCreatingItems = new List<CuiElement>();

            if (destroylist.Count > 0)
            {
                foreach (var e in additionalDestroyList)
                {
                    var destroyItems = dataOld.additionalContainer.Where(
                        o => o != e &&
                             o.Parent == e.Parent && Intersect(GetRect(e), GetRect(o)) &&
                             !destroylist.Contains(o)).ToList();
                    destroylist.AddRange(destroyItems);
                    additionalReCreatingItems.AddRange(destroyItems);

                    foreach (var cuiElement in destroyItems)
                    {
                        var recursiveChildren = GetChildsRecursive(dataNew.additionalContainer, cuiElement.Name)
                            .Where(p => !destroylist.Contains(p)).ToList();
                        destroylist.AddRange(recursiveChildren);
                        additionalReCreatingItems.AddRange(recursiveChildren);
                    }

                    var elementChildren = GetChildsRecursive(dataOld.additionalContainer, e.Name);
                    destroylist.AddRange(elementChildren);
                    additionalReCreatingItems.AddRange(elementChildren);
                }

                destroylist = destroylist.Distinct(CuiComparer).ToList();
                destroylist = SortHierarchy(destroylist);
                if (isDebug)
                {
                    Puts($"DestroyListFull => {string.Join(", ", destroylist.Select(p => p.Name))}");
                }

                for (var i = destroylist.Count - 1; i >= 0; i--)
                {
                    CuiHelper.DestroyUi(player, destroylist[i].Name);
                }
            }

            var createlist = new List<CuiElement>();
            foreach (var arg in changedArgs)
            {
                createlist.AddRange(func.cacheArgs[arg]);
            }

            // Additional container
            var additionalCreateList =
                dataNew.additionalContainer.Except(dataOld.additionalContainer, CuiComparer).ToList();

            if (isDebug)
            {
                Puts($"AdditionalCreateList => {string.Join(", ", additionalCreateList.Select(p => p.Name))}");
            }

            createlist.AddRange(additionalCreateList);
            foreach (var e in additionalCreateList)
            {
                var addedItems = dataNew.additionalContainer.Where(
                    o => o != e &&
                         o.Parent == e.Parent && Intersect(GetRect(e), GetRect(o)) &&
                         !createlist.Contains(o)).ToList();
                createlist.AddRange(addedItems);

                foreach (var cuiElement in addedItems)
                {
                    createlist.AddRange(
                        GetChildsRecursive(dataNew.additionalContainer, cuiElement.Name)
                            .Where(p => !createlist.Contains(p)));
                }

                createlist.AddRange(GetChildsRecursive(dataNew.additionalContainer, e.Name));
            }

            createlist.AddRange(additionalReCreatingItems);

            if (createlist.Count > 0)
            {
                createlist = createlist.Distinct(CuiComparer).ToList();
                createlist = SortHierarchy(createlist);
                if (isDebug)
                {
                    Puts($"CreateListFull => {string.Join(", ", createlist.Select(p => p.Name))}");
                }

                var json = CuiHelper.ToJson(createlist);
                if (dataNew.args.Length > 0)
                {
                    json = HandleArgs(json, dataNew.args);
                }

                CacheUI(player.userID, json);
                CuiHelper.AddUi(player, json);
                if (isDebug)
                {
                    Puts(json);
                    drawCache++;
                }
            }
        }

        private void GetHierarchy(CuiElement element, List<CuiElement> function, List<CuiElement> hierarchy = null)
        {
            if (hierarchy == null)
            {
                hierarchy = new CuiElementContainer();
            }

            hierarchy.Add(element);

            var elementChilds = function.Where(child => child.Parent == element.Name).ToList();
            if (elementChilds.Count <= 0)
            {
                return;
            }

            foreach (var child in elementChilds)
            {
                GetHierarchy(child, function, hierarchy);
            }
        }

        private int GetDept(CuiElement obj)
        {
            if (obj == null || obj.Parent == "Hud" || obj.Parent == "Overlay")
            {
                return 0;
            }

            return GetDept(sortContainer.Find(p => p.Name == obj.Parent)) + 1;
        }

        private List<CuiElement> sortContainer;

        private List<CuiElement> SortHierarchy(List<CuiElement> container) => SortHierarchy(container, container);

        private List<CuiElement> SortHierarchy(List<CuiElement> container, List<CuiElement> allElements)
        {
            sortContainer = allElements;
            return container.OrderBy(GetDept).ToList();
        }

        private Rect GetRect(CuiElement e)
        {
            var transform = (CuiRectTransformComponent)e.Components.Find(c => c.Type == "RectTransform");
            return IntersectCore.GetRect(transform);
        }


        private bool Intersect(Rect a, Rect b) => IntersectCore.Intersects(a, b);

        public static Vector2 ParseVector(string p)
        {
            var strArrays = p.Split(' ');
            if (strArrays.Length != 2)
            {
                return Vector2.zero;
            }

            return new Vector2(float.Parse(strArrays[0]), float.Parse(strArrays[1]));
        }

        private string HandleArgs(string json, object[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                json = json.Replace("{" + i + "}", args[i].ToString());
            }

            return json;
        }

        private void IDestroyUI(BasePlayer player, UIData data)
        {
            if (player == null)
            {
                PrintError("DestroyUI - player = null");
                return;
            }

            var uid = player.userID;

            var json = functions[data.plugin][data.funcName].GUI;
            if (data.args.Length > 0)
            {
                json = HandleArgs(json, data.args).Replace("$", "");
            }

            var container = CuiHelper.FromJson(json);
            container.AddRange(data.additionalContainer);
            container.Reverse();
            container.ForEach(e =>
            {
                if (e.Name != "AddUI CreatedPanel")
                {
                    CuiHelper.DestroyUi(player, e.Name);
                }
            });

            players[player].Remove(data);
        }

        private void DestroyAllUI(BasePlayer player)
        {
            var data = players[player];
            for (var i = data.Count - 1; i >= 0; i--)
            {
                IDestroyUI(player, data[i]);
            }
        }

        #endregion

        #region [Methods] Draw & Destroy

        internal void DrawUI_Internal(BasePlayer player, string plugin, string funcName, params object[] args)
        {
            DrawUIWIthEx_Internal(player, plugin, funcName, new CuiElementContainer(), args);
        }

        internal void DrawUIWIthEx_Internal(BasePlayer player, string plugin, string funcName,
            CuiElementContainer additionalContainer, params object[] args)
        {
            if (!isLoaded)
            {
                timer.Once(0.1f, () => DrawUI_Internal(player, plugin, funcName, args));
                return;
            }

            var data = new UIData(plugin, funcName, additionalContainer, args);
            Dictionary<string, CuiFunction> pluginFuncs;
            if (!functions.TryGetValue(plugin, out pluginFuncs))
            {
                PrintError(
                    $"Draw UI:\r {plugin} not found for {player.userID}:{player.displayName} \nDebug: {data}");
                return;
            }

            CuiFunction func;
            if (!functions[plugin].TryGetValue(funcName, out func))
            {
                PrintError(
                    $"Draw UI:\r {plugin} doesn't contains \"{funcName}\" {player.userID}:{player.displayName} \nDebug: {data}");
                return;
            }

            IDrawUI(player, data);
        }

        internal void DestroyUI_Internal(BasePlayer player, string plugin, string funcName)
        {
            if (!isLoaded)
            {
                timer.Once(0.1f, () => DestroyUI_Internal(player, plugin, funcName));
                return;
            }

            List<UIData> uiList;
            if (!players.TryGetValue(player, out uiList))
            {
                PrintError(
                    $"Destroy UI:\r{player.userID}:{player.displayName} doesn't have Cui\nDebug: Plugin \"{plugin}\" Function \"{funcName}\"");
                return;
            }

            var uiData = players[player];
            var data = uiData.Find(f => f.plugin == plugin && f.funcName == funcName);
            if (data == null)
            {
                return;
            }

            IDestroyUI(player, data);
        }

        #endregion

        #region Functions

        private IEnumerator LoadFunctions()
        {
            yield return AddImages(imagesDB.ReadObject<Dictionary<string, string>>() ??
                                   new Dictionary<string, string>());

            uiDB.Settings.Converters = new List<JsonConverter> { new CuiFunctionConverter() };
            var funcs = uiDB.ReadObject<Dictionary<string, CuiFunction>>();
            functions = new Dictionary<string, Dictionary<string, CuiFunction>>();

            var comparer = new CuiElementComparer();

            foreach (var funcPair in funcs)
            {
                var func = funcPair.Key;
                string plugin, funcName;
                SplitFunc(func, out plugin, out funcName);

                if (!functions.ContainsKey(plugin))
                {
                    functions[plugin] = new Dictionary<string, CuiFunction>();
                }

                functions[plugin].Add(funcName, funcPair.Value);

                var function = functions[plugin][funcName];
                var argc = function.argc();
                var json = function.GUI;
                if (string.IsNullOrEmpty(json))
                {
                    continue;
                }

                var elements = CuiHelper.FromJson(json);

                foreach (var e in elements)
                {
                    var component = e.Components.FirstOrDefault(c => c.Type == "UnityEngine.UI.RawImage");
                    var rawImage = component as CuiRawImageComponent;
                    if (!string.IsNullOrEmpty(rawImage?.Png))
                    {
                        rawImage.Sprite = "assets/content/textures/generic/fulltransparent.tga";

                        if (rawImage.Png.StartsWith("{"))
                        {
                            // if (rawImage.Png == "{colon}")
                            // {
                            //     rawImage.Png = GetImage("colon");
                            // }
                        }
                        else
                        {
                            var loadingTask = AddImageCoroutine(rawImage.Png, rawImage.Png);
                            while (loadingTask.MoveNext())
                            {
                                yield return loadingTask.Current;
                            }

                            rawImage.Png = GetImage(rawImage.Png);
                        }
                    }
                    else if (rawImage != null && string.IsNullOrEmpty(rawImage.Url) &&
                             rawImage.Sprite == "Assets/Icons/rust.png")
                    {
                        rawImage.Sprite = "assets/content/ui/ui.background.tile.psd";
                    }
                }

                function.GUI = json = CuiHelper.ToJson(elements);

                var jsonArgs = json;
                if (argc == 0)
                {
                    function.cacheArgs.Add(elements);
                    continue;
                }

                for (var j = 0; j < argc; j++)
                {
                    jsonArgs = jsonArgs.Replace("{" + j + "}", "");
                }

                var elementsArgs = CuiHelper.FromJson(jsonArgs);

                var argEleement = elementsArgs.Except(elements, comparer).ToList();
                var changedElements = elements.Except(elementsArgs, comparer).ToList();

                var argsElements = changedElements
                    .Select(element => CuiHelper.ToJson(new List<CuiElement> { element })).ToList();
                var argNumbers = new List<int>();
                for (var j = 0; j < argc; j++)
                    for (var k = 0; k < argsElements.Count; k++)
                    {
                        if (argsElements[k].Contains("{" + j + "}"))
                        {
                            argNumbers.Add(k);
                        }
                    }

                for (var j = 0; j < argNumbers.Count; j++)
                {
                    var e = changedElements[argNumbers[j]];
                    var argsReferences = elements.Where(
                        o => o != e &&
                             o.Parent == e.Parent && Intersect(GetRect(e), GetRect(o)) &&
                             !argEleement.Contains(o)).ToList();
                    argsReferences.Insert(0, e);
                    var newReferences = new List<CuiElement>();
                    for (var index = 0; index < argsReferences.Count; index++)
                    {
                        var element = argsReferences[index];
                        newReferences.AddRange(GetChildsRecursive(elements, element.Name));
                    }

                    argsReferences.AddRange(newReferences);
                    argsReferences = argsReferences.Distinct(comparer).ToList();
                    argsReferences = SortHierarchy(argsReferences, elements);
                    function.cacheArgs.Add(argsReferences);
                }
            }

            foreach (var pluginFunctions in functions)
                foreach (var func in pluginFunctions.Value)
                {
                    var funcAspect = func.Value.GUI;
                    if (string.IsNullOrEmpty(funcAspect))
                    {
                        continue;
                    }

                    var elements = CuiHelper.FromJson(funcAspect);
                }

            Interface.Oxide.LogInfo($"[Core] [CuiGenerator] Loaded <{funcs.Count}> functions.");
            onCuiGeneratorInitialized();
        }

        private List<CuiElement> GetChildsRecursive(List<CuiElement> elements, string name)
        {
            var childs = new List<CuiElement>();
            foreach (var element in elements)
            {
                if (element.Parent == name)
                {
                    childs.Add(element);
                    childs.AddRange(GetChildsRecursive(elements, element.Name));
                }
            }

            return childs;
        }

        private void SplitFunc(string func, out string plugin, out string funcName)
        {
            plugin = funcName = string.Empty;
            try
            {
                plugin = func.Substring(0, func.IndexOf('_'));
                funcName = func.Substring(func.IndexOf('_') + 1, func.Length - func.IndexOf('_') - 1);
            }
            catch (Exception e)
            {
                Debug.LogError($"'{func}' dont have separator");
            }
        }

        #endregion

        private void DrawUI(BasePlayer player, string plugin, string funcName, params object[] args)
        {
            DrawUI_Internal(player, plugin, funcName, args);
        }

        private void DrawUIWIthEx(BasePlayer player, string plugin,
                                  string funcName,
                                  Oxide.Game.Rust.Cui.CuiElementContainer additionalContainer, params object[] args)
        {
            DrawUIWIthEx_Internal(player, plugin, funcName, new CuiElementContainer(CuiHelper.FromJson(additionalContainer.ToJson())), args);
        }

        private void DestroyUI(BasePlayer player, string plugin, string funcName)
        {
            DestroyUI_Internal(player, plugin, funcName);
        }

        [ConsoleCommand("cui.debug")]
        private void cmdDebug(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                return;
            }

            isDebug = !isDebug;
            Puts($"Cui Debug: {isDebug}");
        }

        [ConsoleCommand("cui.args")]
        private void cmdArgs(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                return;
            }

            if (arg.HasArgs(2))
            {
                var i = 0;
                foreach (var argumentRefs in functions[arg.Args[0]][arg.Args[1]].cacheArgs)
                {
                    Puts($"Argument {i++}:\n{CuiHelper.ToJson(argumentRefs, true)}");
                }
            }
        }

        public static class IntersectCore
        {
            public static Rect GetRect(CuiRectTransformComponent transform)
            {
                var anchorMin = Vector2Ex.Parse(transform.AnchorMin);
                var anchorMax = Vector2Ex.Parse(transform.AnchorMax);

                var offsetMin = Vector2Ex.Parse(transform.OffsetMin);
                var offsetMax = Vector2Ex.Parse(transform.OffsetMax);

                var rectMin = anchorMin + offsetMin * 0.00078125F;
                var rectMax = anchorMax + offsetMax * 0.001388889F;
                return new Rect(rectMin, rectMax - rectMin);
            }

            private static bool ValueInRange(float value, float min, float max) => value >= min && value <= max;

            public static bool Intersects(Rect a, Rect b)
            {
                // Check bounds
                /*
                var xOverlap = ValueInRange(a.x, b.x, b.x + b.width) ||
                               ValueInRange(b.x, a.x, a.x + a.width);
    
                var yOverlap = ValueInRange(a.y, b.y, b.y + b.height) ||
                               ValueInRange(b.y, a.y, a.y + a.height);
                */

                // Check center overlap
                var centerxOverlap = ValueInRange(a.center.x, b.x, b.x + b.width) ||
                                     ValueInRange(b.center.x, a.x, a.x + a.width);
                var centeryOverlap = ValueInRange(a.center.y, b.y, b.y + b.height) ||
                                     ValueInRange(b.center.y, a.y, a.y + a.height);

                return centerxOverlap && centeryOverlap;
            }
        }
        internal class UIData
        {
            public CuiElementContainer additionalContainer;
            public object[] args;
            public string funcName;
            public string plugin;

            public UIData(string plugin, string funcName, params object[] args)
            {
                this.plugin = plugin;
                this.funcName = funcName;
                this.args = args;
            }

            public UIData(string plugin, string funcName, CuiElementContainer additionalContainer, params object[] args)
            {
                this.plugin = plugin;
                this.funcName = funcName;
                this.additionalContainer = additionalContainer;
                this.args = args;
            }

            public override string ToString()
            {
                return
                    $"Plugin \"{plugin}\" Function \"{funcName}\" Args \"{string.Join(", ", args.Select(arg => arg.ToString()).ToArray())}\"";
            }
        }
        public class ComponentConverter : JsonConverter
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                var jobject = JObject.Load(reader);
                Type type;
                switch (jobject["type"].ToString())
                {
                    case "NeedsCursor":
                        type = typeof(CuiNeedsCursorComponent);
                        break;
                    case "Countdown":
                        type = typeof(CuiCountdownComponent);
                        break;
                    case "RectTransform":
                        type = typeof(CuiRectTransformComponent);
                        break;
                    case "UnityEngine.UI.Button":
                        type = typeof(CuiButtonComponent);
                        break;
                    case "UnityEngine.UI.Image":
                        type = typeof(CuiImageComponent);
                        break;
                    case "UnityEngine.UI.InputField":
                        type = typeof(CuiInputFieldComponent);
                        break;
                    case "UnityEngine.UI.Outline":
                        type = typeof(CuiOutlineComponent);
                        break;
                    case "UnityEngine.UI.RawImage":
                        type = typeof(CuiRawImageComponent);
                        break;
                    case "UnityEngine.UI.Text":
                        type = typeof(CuiTextComponent);
                        break;
                    default:
                        return null;
                }

                var instance = Activator.CreateInstance(type);
                serializer.Populate(jobject.CreateReader(), instance);
                return instance;
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(ICuiComponent);
        }
        public class CuiButton
        {
            public CuiButtonComponent Button { get; } = new CuiButtonComponent();
            public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
            public CuiTextComponent Text { get; } = new CuiTextComponent();
            public float FadeOut { get; set; }
        }

        public class CuiPanel
        {
            public CuiImageComponent Image { get; set; } = new CuiImageComponent();
            public CuiRawImageComponent RawImage { get; set; }
            public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
            public bool CursorEnabled { get; set; }
            public float FadeOut { get; set; }
        }

        public class CuiLabel
        {
            public CuiTextComponent Text { get; } = new CuiTextComponent();
            public CuiRectTransformComponent RectTransform { get; } = new CuiRectTransformComponent();
            public float FadeOut { get; set; }
        }


        public class CuiElementContainer : List<CuiElement>
        {
            public CuiElementContainer() { }

            public CuiElementContainer(List<CuiElement> elements)
                : base(elements)
            { }

            public string Add(CuiButton button, string parent = "Hud", string name = null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = CuiHelper.GetGuid();
                }

                Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = button.FadeOut,
                    Components =
                    {
                        button.Button,
                        button.RectTransform
                    }
                });
                if (!string.IsNullOrEmpty(button.Text.Text))
                {
                    Add(new CuiElement
                    {
                        Parent = name,
                        FadeOut = button.FadeOut,
                        Components =
                        {
                            button.Text,
                            new CuiRectTransformComponent()
                        }
                    });
                }

                return name;
            }

            public string Add(CuiLabel label, string parent = "Hud", string name = null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = CuiHelper.GetGuid();
                }

                Add(new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = label.FadeOut,
                    Components =
                    {
                        label.Text,
                        label.RectTransform
                    }
                });
                return name;
            }

            public string Add(CuiPanel panel, string parent = "Hud", string name = null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = CuiHelper.GetGuid();
                }

                var element = new CuiElement
                {
                    Name = name,
                    Parent = parent,
                    FadeOut = panel.FadeOut
                };
                if (panel.Image != null)
                {
                    element.Components.Add(panel.Image);
                }

                if (panel.RawImage != null)
                {
                    element.Components.Add(panel.RawImage);
                }

                element.Components.Add(panel.RectTransform);
                if (panel.CursorEnabled)
                {
                    element.Components.Add(new CuiNeedsCursorComponent());
                }

                Add(element);
                return name;
            }

            public string ToJson() => ToString();

            public override string ToString() => CuiHelper.ToJson(this);
        }


        public static class CuiHelper
        {
            public static string ToJson(List<CuiElement> elements, bool format = false) =>
                JsonConvert.SerializeObject(elements, format ? Formatting.Indented : Formatting.None,
                                            new JsonSerializerSettings
                                            {
                                                DefaultValueHandling = DefaultValueHandling.Ignore
                                            }).Replace("\\n", "\n");

            public static List<CuiElement> FromJson(string json) =>
                JsonConvert.DeserializeObject<List<CuiElement>>(json);

            public static string GetGuid() => Guid.NewGuid().ToString().Replace("-", string.Empty);

            public static bool AddUi(BasePlayer player, List<CuiElement> elements) => AddUi(player, ToJson(elements));

            public static bool AddUi(BasePlayer player, string json)
            {
                if (player?.net != null && Interface.CallHook("CanUseUI", player, json) == null)
                {
                    CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo { connection = player.net.connection }, null,
                                                               "AddUI", json);
                    return true;
                }

                return false;
            }

            public static bool DestroyUi(BasePlayer player, string elem)
            {
                if (player?.net != null)
                {
                    Interface.CallHook("OnDestroyUI", player, elem);
                    CommunityEntity.ServerInstance.ClientRPCEx(new SendInfo { connection = player.net.connection }, null,
                                                               "DestroyUI", elem);
                    return true;
                }

                return false;
            }

            public static void SetColor(ICuiColor elem, Color color)
            {
                elem.Color = $"{color.r} {color.g} {color.b} {color.a}";
            }

            public static Color GetColor(ICuiColor elem) => ColorEx.Parse(elem.Color);
        }
        public interface ICuiColor
        {
            [DefaultValue("1.0 1.0 1.0 1.0")]
            [JsonProperty("color")]
            string Color { get; set; }
        }
        public class CuiButtonComponent : ICuiComponent, ICuiColor
        {
            public string Type => "UnityEngine.UI.Button";

            [JsonProperty("command")]
            public string Command { get; set; }

            [JsonProperty("close")]
            public string Close { get; set; }

            // The sprite that is used to render this image.
            [DefaultValue("Assets/Content/UI/UI.Background.Tile.psd")]
            [JsonProperty("sprite")]
            public string Sprite { get; set; } = "Assets/Content/UI/UI.Background.Tile.psd";

            // The Material set by the player.
            [DefaultValue("Assets/Icons/IconMaterial.mat")]
            [JsonProperty("material")]
            public string Material { get; set; } = "Assets/Icons/IconMaterial.mat";

            public string Color { get; set; } = "1.0 1.0 1.0 1.0";

            // How the Image is draw.
            [DefaultValue(Image.Type.Simple)]
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("imagetype")]
            public Image.Type ImageType { get; set; } = Image.Type.Simple;

            [JsonProperty("fadeIn")]
            public float FadeIn { get; set; }
        }
        public class CuiCountdownComponent : ICuiComponent
        {
            public string Type => "Countdown";

            [JsonProperty("startTime"), DefaultValue(0)]
            public int StartTime = 0;

            [JsonProperty("endTime"), DefaultValue(0)]
            public int EndTime = 0;

            [JsonProperty("step"), DefaultValue(1)]
            public int Step = 1;

            [JsonProperty("command"), DefaultValue("")]
            public string Command = "";
        }
        public class CuiElement
        {
            [DefaultValue("AddUI CreatedPanel")]
            [JsonProperty("name")]
            public string Name { get; set; } = "AddUI CreatedPanel";

            [JsonProperty("parent")]
            public string Parent { get; set; } = "Hud";

            [JsonProperty("components")]
            public List<ICuiComponent> Components { get; } = new List<ICuiComponent>();

            [JsonProperty("fadeOut")]
            public float FadeOut { get; set; }
        }
        public class CuiImageComponent : ICuiComponent, ICuiColor
        {
            public string Type => "UnityEngine.UI.Image";

            [DefaultValue("Assets/Content/UI/UI.Background.Tile.psd")]
            [JsonProperty("sprite")]
            public string Sprite { get; set; } = "Assets/Content/UI/UI.Background.Tile.psd";

            [DefaultValue("Assets/Icons/IconMaterial.mat")]
            [JsonProperty("material")]
            public string Material { get; set; } = "Assets/Icons/IconMaterial.mat";

            public string Color { get; set; } = "1.0 1.0 1.0 1.0";

            [DefaultValue(Image.Type.Simple)]
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("imagetype")]
            public Image.Type ImageType { get; set; } = Image.Type.Simple;

            [JsonProperty("png")]
            public string Png { get; set; }

            [JsonProperty("fadeIn")]
            public float FadeIn { get; set; }
        }
        public class CuiInputFieldComponent : ICuiComponent, ICuiColor
        {
            public string Type => "UnityEngine.UI.InputField";

            // The string value this text will display.
            [DefaultValue("Text")]
            [JsonProperty("text")]
            public string Text { get; set; } = "Text";

            // The size that the Font should render at.
            [DefaultValue(14)]
            [JsonProperty("fontSize")]
            public int FontSize { get; set; } = 14;

            // The Font used by the text.
            [DefaultValue("RobotoCondensed-Bold.ttf")]
            [JsonProperty("font")]
            public string Font { get; set; } = "RobotoCondensed-Bold.ttf";

            // The positioning of the text reliative to its RectTransform.
            [DefaultValue(TextAnchor.UpperLeft)]
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("align")]
            public TextAnchor Align { get; set; } = TextAnchor.UpperLeft;

            public string Color { get; set; } = "1.0 1.0 1.0 1.0";

            [DefaultValue(100)]
            [JsonProperty("characterLimit")]
            public int CharsLimit { get; set; } = 100;

            [JsonProperty("command")]
            public string Command { get; set; }

            [DefaultValue(false)]
            [JsonProperty("password")]
            public bool IsPassword { get; set; }
        }
        public class CuiNeedsCursorComponent : ICuiComponent
        {
            public string Type => "NeedsCursor";
        }
        public class CuiOutlineComponent : ICuiComponent, ICuiColor
        {
            public string Type => "UnityEngine.UI.Outline";

            // Color for the effect.
            public string Color { get; set; } = "1.0 1.0 1.0 1.0";

            // How far is the shadow from the graphic.
            [DefaultValue("1.0 -1.0")]
            [JsonProperty("distance")]
            public string Distance { get; set; } = "1.0 -1.0";

            // Should the shadow inherit the alpha from the graphic?
            [DefaultValue(false)]
            [JsonProperty("useGraphicAlpha")]
            public bool UseGraphicAlpha { get; set; }
        }
        public class CuiRawImageComponent : ICuiComponent, ICuiColor
        {
            public string Type => "UnityEngine.UI.RawImage";

            [DefaultValue("Assets/Icons/rust.png")]
            [JsonProperty("sprite")]
            public string Sprite { get; set; } = "Assets/Icons/rust.png";

            public string Color { get; set; } = "1.0 1.0 1.0 1.0";

            [JsonProperty("material")]
            public string Material { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("png")]
            public string Png { get; set; }

            [JsonProperty("fadeIn")]
            public float FadeIn { get; set; }
        }
        public class CuiRectTransformComponent : ICuiComponent
        {
            public string Type => "RectTransform";

            // The normalized position in the parent RectTransform that the lower left corner is anchored to.
            [DefaultValue("0.0 0.0")]
            [JsonProperty("anchormin")]
            public string AnchorMin { get; set; } = "0.0 0.0";

            // The normalized position in the parent RectTransform that the upper right corner is anchored to.
            [DefaultValue("1.0 1.0")]
            [JsonProperty("anchormax")]
            public string AnchorMax { get; set; } = "1.0 1.0";

            // The offset of the lower left corner of the rectangle relative to the lower left anchor.
            [DefaultValue("0.0 0.0")]
            [JsonProperty("offsetmin")]
            public string OffsetMin { get; set; } = "0.0 0.0";

            // The offset of the upper right corner of the rectangle relative to the upper right anchor.
            [DefaultValue("0.0 0.0")]
            [JsonProperty("offsetmax")]
            public string OffsetMax { get; set; } = "0 0";
        }
        public class CuiTextComponent : ICuiComponent, ICuiColor
        {
            public string Type => "UnityEngine.UI.Text";

            // The string value this text will display.
            [DefaultValue("Text")]
            [JsonProperty("text")]
            public string Text { get; set; } = "Text";

            // The size that the Font should render at.
            [DefaultValue(14)]
            [JsonProperty("fontSize")]
            public int FontSize { get; set; } = 14;

            // The Font used by the text.
            [DefaultValue("RobotoCondensed-Bold.ttf")]
            [JsonProperty("font")]
            public string Font { get; set; } = "RobotoCondensed-Bold.ttf";

            // The positioning of the text reliative to its RectTransform.
            [DefaultValue(TextAnchor.UpperLeft)]
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("align")]
            public TextAnchor Align { get; set; } = TextAnchor.UpperLeft;

            public string Color { get; set; } = "1.0 1.0 1.0 1.0";

            [JsonProperty("fadeIn")]
            public float FadeIn { get; set; }
        }
        [JsonConverter(typeof(ComponentConverter))]
        public interface ICuiComponent
        {
            [JsonProperty("type")]
            string Type { get; }
        }
        [PluginReference] private Plugin ImageLibrary;

        bool AddImage(string url, string imageName, ulong imageId = 0, Action callback = null)
        {
            if (ImageLibrary == null)
                return false;
            return (bool)ImageLibrary.Call("AddImage", url, imageName, imageId, callback);
        }

        string GetImage(string imageName, ulong imageId = 0, bool returnUrl = false)
        {
            return (string)ImageLibrary?.Call("GetImage", imageName, imageId, returnUrl);
        }

        bool HasImage(string imageName, ulong imageId = 0)
        {
            if (ImageLibrary == null)
                return false;
            return (bool)ImageLibrary.Call("HasImage", imageName, imageId);
        }

        IEnumerator AddImageCoroutine(string imagename, string url)
        {
            AddImage(url, imagename);

            while (HasImage(imagename) == false)
            {
                yield return CoroutineEx.waitForSeconds(.1f);
            }
        }

        IEnumerator AddImages(Dictionary<string, string> images)
        {
            foreach (var keyImage in images)
            {
                AddImage(keyImage.Key, keyImage.Value);
            }

            while (images.All(img => HasImage(img.Key)) == false)
            {
                yield return CoroutineEx.waitForSeconds(.1f);
            }
        }
    }
}