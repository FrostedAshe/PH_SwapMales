using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Character;
using KKAPI.Chara;

namespace PH_SwapMales
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
    public class SwapMales : BaseUnityPlugin
    {
        public const string GUID = "frostedashe.swapmales";
        public const string PluginName = "PH_SwapMales";
        public const string Version = "1.0.0";

        private const string SECTION_HOTKEYS = "Keyboard Shortcuts";
        private ConfigEntry<KeyboardShortcut> SwapMalesHotKey { get; set; }
        private ConfigEntry<KeyboardShortcut> SwapMalesResetHotKey { get; set; }
        private Dictionary<MALE_ID, CustomParameter> originalMaleParams;
        private Dictionary<MALE_ID, MALE_ID> currentParamMap;
        private bool cyclingMales = false;
        private bool resettingMales = false;
        private Human lastReloadedHuman = null;

        private void SetMaleMap(MALE_ID id, MALE_ID mappedValue)
        {
            if(currentParamMap.ContainsKey(id) ) {
                currentParamMap[id] = mappedValue;
            }
            else {
                currentParamMap.Add(id, mappedValue);
            }
        }

        private void Awake()
        {
            SwapMalesHotKey = Config.Bind(SECTION_HOTKEYS, "Swap the males in current scene", new KeyboardShortcut(KeyCode.KeypadMultiply));
            SwapMalesResetHotKey = Config.Bind(SECTION_HOTKEYS, "Reset all males to default", new KeyboardShortcut(KeyCode.KeypadDivide));
            originalMaleParams = new Dictionary<MALE_ID, CustomParameter>();
            currentParamMap = new Dictionary<MALE_ID, MALE_ID>();
            cyclingMales = false;
            resettingMales = false;
            
            CharacterApi.CharacterReloaded += OnCharacterReload;
            Harmony.CreateAndPatchAll(GetType());
        }

        private void Update()
        {
            // Reset all males to original appearance
            if(SwapMalesResetHotKey.Value.IsDown())
            {
                Male[] males = FindObjectsOfType<Male>();

                resettingMales = true;
                // Load all original appearances
                for(int i = 0; i < males.Length; i++)
                {
                    if(originalMaleParams.ContainsKey(males[i].MaleID))
                    {
                        if(males[i].MaleID != currentParamMap[males[i].MaleID])
                        {
                            males[i].Load(originalMaleParams[males[i].MaleID]);
                        }
                    }
                }

                // Reset the parameter map
                for(int i = 0; i < currentParamMap.Count; i++)
                {
                    currentParamMap[currentParamMap.ElementAt(i).Key] = currentParamMap.ElementAt(i).Key;
                }
                resettingMales = false;
            }

            // Swap the appearance of all males
            if(SwapMalesHotKey.Value.IsDown())
            {
                // Get all males in the scene
                Male[] males = FindObjectsOfType<Male>();

                // Do nothing if there is only one male
                if(males.Length == 1)
                    return;
                
                // Cache all male's parameters in a temporary list
                var customParams = new Dictionary<MALE_ID, CustomParameter>();
                for(int i = 0; i < males.Length; i++)
                {
                    customParams.Add(males[i].MaleID, new CustomParameter(males[i].customParam));
                }
                
                // This flag is used by OnCharacterReload() to avoid reseting parameters when we're cycling
                cyclingMales = true;
                
                // Cycle the males using the temporary list and update the parameter map
                males[males.Length - 1].Load(customParams.ElementAt(0).Value);
                MALE_ID firstParam = currentParamMap[males[0].MaleID];
                for(int i = 0; i < males.Length - 1; i++)
                {
                    males[i].Load(customParams.ElementAt(i + 1).Value);
                    currentParamMap[males[i].MaleID] = currentParamMap[males[i + 1].MaleID];
                }
                currentParamMap[males[males.Length - 1].MaleID] = firstParam;
                
                cyclingMales = false;
            }
        }

        public void OnCharacterReload(object sender, CharaReloadEventArgs args)
        {
            // Do not do anything if we are the ones who triggered a character reload
            if(resettingMales || cyclingMales) return;

            if(args == null) return;
            if(args.ReloadedCharacter == lastReloadedHuman) return;
            if(args.ReloadedCharacter.sex != SEX.MALE) return;

            Male[] males = FindObjectsOfType<Male>();
            int reloadedCharIndex = 0;

            // Find the character that just reloaded
            for(int i = 0; i < males.Length; i++)
            {
                if(males[i].GetInstanceID() == args.ReloadedCharacter.GetInstanceID())
                {
                    reloadedCharIndex = i;
                }
            }

            // If guy is loaded for the first time, add his parameters to our list of known original parameters
            // Also add the guy to the current male map
            if(! originalMaleParams.ContainsKey(males[reloadedCharIndex].MaleID) ) {
                originalMaleParams.Add(males[reloadedCharIndex].MaleID, new CustomParameter(males[reloadedCharIndex].customParam));
                SetMaleMap(males[reloadedCharIndex].MaleID, males[reloadedCharIndex].MaleID);
            }
            else // Set his appearance according to the map
            {
                if(males[reloadedCharIndex].MaleID != currentParamMap[males[reloadedCharIndex].MaleID])
                {
                    males[reloadedCharIndex].Load(originalMaleParams[currentParamMap[males[reloadedCharIndex].MaleID]]);
                }
            }

            lastReloadedHuman = args.ReloadedCharacter;
        }
    }
}
