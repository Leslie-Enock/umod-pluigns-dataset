using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Assets Checker", "SettLe", "1.0.5")]
    [Description("Extracts changes from the game’s assets and writes them to sorted lists.")]
    public class AssetsChecker : RustPlugin
    {
        private GameManifest.PooledString[] _manifest;
        private bool _inProcess;
        private List<string> _allPrefabs, 
            _allAssets,
            _allImages,
            _allOther,
            _oldPrefabs,
            _oldAssets,
            _oldImages,
            _oldOther,
            _lostPrefabs,
            _lostAssets,
            _lostImages,
            _lostOther,
            _newPrefabs,
            _newAssets,
            _newImages,
            _newOther;

        private void OnServerInitialized()
        {
            _manifest = GameManifest.Current.pooledStrings;
        }
        
        private static void LoadData<T>(ref T data, string file)
        {
            data = Core.Interface.Oxide.DataFileSystem.ReadObject<T>($"AssetsChecker/{file}");
        }
		
        private static void SaveData<T>(T data, string file)
        {
            Core.Interface.Oxide.DataFileSystem.WriteObject($"AssetsChecker/{file}", data);
        }

        private void Unload()
        {
            Clear();
        }

        private void LoadOld()
        {
            LoadData(ref _oldPrefabs, "LastAssembly/Prefabs");
            LoadData(ref _oldAssets, "LastAssembly/Assets");
            LoadData(ref _oldImages, "LastAssembly/Images");
            LoadData(ref _oldOther, "LastAssembly/Other");
        }
        
        private void SaveAll()
        {
            SaveData(_allPrefabs, "LastAssembly/Prefabs");
            SaveData(_allAssets, "LastAssembly/Assets");
            SaveData(_allImages, "LastAssembly/Images");
            SaveData(_allOther, "LastAssembly/Other");
        }
        
        private void SaveLost(string folder)
        {
            SaveData(_lostPrefabs, $"{folder}/Lost/Prefabs");
            SaveData(_lostAssets, $"{folder}/Lost/Assets");
            SaveData(_lostImages, $"{folder}/Lost/Images");
            SaveData(_lostOther, $"{folder}/Lost/Other");
        }
        
        private void SaveNew(string folder)
        {
            SaveData(_newPrefabs, $"{folder}/New/Prefabs");
            SaveData(_newAssets, $"{folder}/New/Assets");
            SaveData(_newImages, $"{folder}/New/Images");
            SaveData(_newOther, $"{folder}/New/Other");
        }
        
        private void Clear()
        {
            _allPrefabs = null;
            _allAssets = null;
            _allImages = null;
            _allOther = null;
            _oldPrefabs = null;
            _oldAssets = null;
            _oldImages = null;
            _oldOther = null;
            _lostPrefabs = null;
            _lostAssets = null;
            _lostImages = null;
            _lostOther = null;
            _newPrefabs = null;
            _newAssets = null;
            _newImages = null;
            _newOther = null;
        }
        
        private void Search()
        {
            _inProcess = true;
            _allPrefabs = new List<string>();
            _allAssets = new List<string>();
            _allImages = new List<string>();
            _allOther = new List<string>();
                
            foreach (GameManifest.PooledString asset in _manifest)
            {
                if (asset.str.EndsWith(".prefab"))
                    _allPrefabs.Add(asset.str);
                else if (asset.str.EndsWith(".asset"))
                    _allAssets.Add(asset.str);
                else if (asset.str.EndsWith(".png"))
                    _allImages.Add(asset.str);
                else
                    _allOther.Add(asset.str);
            }
            
            if (_oldPrefabs.IsNullOrEmpty() || _oldAssets.IsNullOrEmpty() || _oldImages.IsNullOrEmpty() || _oldOther.IsNullOrEmpty())
            {
                PrintWarning("Search for changes is not possible due to absence of lists of old build.\nNew lists have been created for the current build in the AssetsChecker/LastAssembly folder.");
            }
            else
            {
                Puts("The process of searching for changes in the game's assets is started.");
                
                _newPrefabs = new List<string>();
                _newAssets = new List<string>();
                _newImages = new List<string>();
                _newOther = new List<string>();
                
                foreach (var prefab in _allPrefabs)
                {
                    if (!_oldPrefabs.Contains(prefab))
                    {
                        _newPrefabs.Add(prefab);
                    }
                }
                
                foreach (var prefab in _allAssets)
                {
                    if (!_oldAssets.Contains(prefab))
                    {
                        _newAssets.Add(prefab);
                    }
                }
                
                foreach (var prefab in _allImages)
                {
                    if (!_oldImages.Contains(prefab))
                    {
                        _newImages.Add(prefab);
                    }
                }
                
                foreach (var prefab in _allOther)
                {
                    if (!_oldOther.Contains(prefab))
                    {
                        _newOther.Add(prefab);
                    }
                }
                
                _lostPrefabs = new List<string>();
                _lostAssets = new List<string>();
                _lostImages = new List<string>();
                _lostOther = new List<string>();
                
                foreach (var prefab in _oldPrefabs)
                {
                    if (!_allPrefabs.Contains(prefab))
                    {
                        _lostPrefabs.Add(prefab);
                    }
                }
                
                foreach (var prefab in _oldAssets)
                {
                    if (!_allAssets.Contains(prefab))
                    {
                        _lostAssets.Add(prefab);
                    }
                }
                
                foreach (var prefab in _oldImages)
                {
                    if (!_allImages.Contains(prefab))
                    {
                        _lostImages.Add(prefab);
                    }
                }
                
                foreach (var prefab in _oldOther)
                {
                    if (!_allOther.Contains(prefab))
                    {
                        _lostOther.Add(prefab);
                    }
                }
				
                if (_newPrefabs.Count > 0 || _newAssets.Count > 0 || _newImages.Count > 0 || _newOther.Count > 0)
                {
					var now = DateTime.Now;
					var folder = $"{now.Month.ToString()}.{now.Day.ToString()}";
                    SaveNew(folder);
                    Puts($"Found new: {_newPrefabs.Count.ToString()} prefabs, {_newAssets.Count.ToString()} assets, {_newImages.Count.ToString()} images, {_newOther.Count.ToString()} other.\nThe lists is saved to a folder: Oxide/data/AssetsChecker/{folder}/New");
					
					if (_lostPrefabs.Count > 0 || _lostAssets.Count > 0 || _lostImages.Count > 0 || _lostOther.Count > 0)
					{
						SaveLost(folder);
						Puts($"Found removed or renamed: {_lostPrefabs.Count.ToString()} prefabs, {_lostAssets.Count.ToString()} assets, {_lostImages.Count.ToString()} images, {_lostOther.Count.ToString()} other.\nThe lists is saved to a folder: Oxide/data/AssetsChecker/{folder}/Lost");
					}
				}
                else PrintWarning("Changes not found.");
            }
            
            SaveAll();
            Clear();
            _inProcess = false;
        }

        [ConsoleCommand("check_assets")]
        private void CommandCheckAssets(ConsoleSystem.Arg console)
        {
            if (!console.IsServerside)
                return;
            
            if (_inProcess)
            {
                PrintError("The process is already running.");
                return;
            }
            
            LoadOld();
            NextFrame(() => Search());
        }
    }
}