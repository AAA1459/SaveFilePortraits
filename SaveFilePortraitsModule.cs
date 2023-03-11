﻿using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using Monocle;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Celeste.Mod.SaveFilePortraits {
    public class SaveFilePortraitsModule : EverestModule {
        public class SaveData : EverestModuleSaveData {
            public string Portrait { get; set; } = "portrait_madeline";
            public string Animation { get; set; } = "idle_normal";
        }

        public override Type SaveDataType => typeof(SaveData);
        public SaveData ModSaveData => (SaveData) _SaveData;

        public static List<Tuple<string, string>> ExistingPortraits;

        private PortraitPicker portraitPicker;

        public override void Load() {
            On.Celeste.GFX.LoadData += onGFXLoadData;
            IL.Celeste.OuiFileSelectSlot.Setup += onOuiFileSelectSetup;
            Everest.Events.FileSelectSlot.OnCreateButtons += onCreateFileSelectSlotButtons;
            IL.Celeste.OuiFileSelectSlot.Update += onFileSelectSlotUpdate;
            IL.Celeste.OuiFileSelectSlot.Render += onFileSelectSlotRender;
            On.Celeste.Overworld.End += onOverworldEnd;
        }

        public override void Unload() {
            On.Celeste.GFX.LoadData -= onGFXLoadData;
            IL.Celeste.OuiFileSelectSlot.Setup -= onOuiFileSelectSetup;
            Everest.Events.FileSelectSlot.OnCreateButtons -= onCreateFileSelectSlotButtons;
            IL.Celeste.OuiFileSelectSlot.Update -= onFileSelectSlotUpdate;
            IL.Celeste.OuiFileSelectSlot.Render -= onFileSelectSlotRender;
            On.Celeste.Overworld.End -= onOverworldEnd;
        }

        private void onGFXLoadData(On.Celeste.GFX.orig_LoadData orig) {
            orig();

            // go through all loaded portraits in GFX.PortraitsSpriteBank and list the ones following the idle_[something] pattern.
            // [something] should not include any _ either, and should have dimensions that fit in the allocated space (:assertivebaddy: doesn't look that great on save files).
            ExistingPortraits = new List<Tuple<string, string>>();
            foreach (string portrait in GFX.PortraitsSpriteBank.SpriteData.Keys) {
                SpriteData sprite = GFX.PortraitsSpriteBank.SpriteData[portrait];
                foreach (string animation in sprite.Sprite.Animations.Keys) {
                    if (animation.StartsWith("idle_") && !animation.Substring(5).Contains("_")
                        && sprite.Sprite.Animations[animation].Frames[0].Height <= 200 && sprite.Sprite.Animations[animation].Frames[0].Width <= 200) {

                        ExistingPortraits.Add(new Tuple<string, string>(portrait, animation));
                    }
                }
            }

            Logger.Log("SaveFilePortraits", $"Found {ExistingPortraits.Count} portraits to pick from.");
        }

        private void onOuiFileSelectSetup(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // replace the **sprite** on save data slots when they're loaded in
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdstr("portrait_madeline"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, OuiFileSelectSlot, string>>((orig, self) => {
                if (self.Exists && !self.Corrupted) {
                    LoadSaveData(self.FileSlot);
                    if (GFX.PortraitsSpriteBank.Has(ModSaveData.Portrait)) {
                        return ModSaveData.Portrait;
                    }
                }
                return orig;
            });

            // replace the **animation** on save data slots when they're loaded in
            cursor.GotoNext(MoveType.After, instr => instr.MatchLdstr("idle_normal"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<string, OuiFileSelectSlot, string>>((orig, self) => {
                if (self.Exists && !self.Corrupted) {
                    if (GFX.PortraitsSpriteBank.Has(ModSaveData.Portrait) && GFX.PortraitsSpriteBank.SpriteData[ModSaveData.Portrait].Sprite.Has(ModSaveData.Animation)) {
                        return ModSaveData.Animation;
                    }
                }
                return orig;
            });
        }

        private void onCreateFileSelectSlotButtons(List<OuiFileSelectSlot.Button> buttons, OuiFileSelectSlot slot, EverestModuleSaveData modSaveData, bool fileExists) {
            // add the portrait picker option to file select slots.
            buttons.Add(portraitPicker = new PortraitPicker(slot, this));
        }

        private void onFileSelectSlotUpdate(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            cursor.GotoNext(MoveType.AfterLabel, instr => instr.MatchRet());

            // I just want to copy paste what OuiFileSelectSlotLevelSetPicker does in OuiFileSelectSlot.Update but that uses half a million private fields
            // so instead of using reflection I'm going to ask for them through IL aaaaa
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("fileSelect", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("buttons", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("buttonIndex", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("tween", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("inputDelay", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Callvirt, typeof(OuiFileSelectSlot).GetMethod("get_selected", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("deleting", BindingFlags.NonPublic | BindingFlags.Instance));

            cursor.EmitDelegate<Action<OuiFileSelectSlot, OuiFileSelect, List<OuiFileSelectSlot.Button>, int, Tween, float, bool, bool>>((self, fileSelect, buttons, buttonIndex,
                tween, inputDelay, selected, deleting) => {
                    if (portraitPicker != null && selected && fileSelect.Selected && fileSelect.Focused &&
                        !self.StartingGame && tween == null && inputDelay <= 0f && !deleting) {

                        // currently highlighted option is the portrait picker, call its Update() method to handle Left and Right presses.
                        portraitPicker.Update(buttons[buttonIndex] == portraitPicker);
                    }
                });
        }

        private void onFileSelectSlotRender(ILContext il) {
            ILCursor cursor = new ILCursor(il);

            // Jump to the point where we go through all buttons.
            cursor.GotoNext(instr => instr.MatchLdarg(0), instr => instr.MatchLdfld<OuiFileSelectSlot>("newGameLevelSetPicker"));

            // get the variable indices of position and i by type, which is a bit cleaner than hard coding them.
            int positionVariableIndex = -1;
            int loopIndexVariableIndex = -1;
            foreach (VariableDefinition var in il.Method.Body.Variables) {
                if (var.VariableType.FullName == "Microsoft.Xna.Framework.Vector2") {
                    positionVariableIndex = var.Index;
                } else if (var.VariableType.FullName == "System.Int32") {
                    loopIndexVariableIndex = var.Index;
                }
            }

            // then get half a million private fields and local variables.
            cursor.Emit(OpCodes.Dup);
            cursor.Emit(OpCodes.Ldloc, positionVariableIndex);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("buttonIndex", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldloc, loopIndexVariableIndex);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("deleting", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, typeof(OuiFileSelectSlot).GetField("wiggler", BindingFlags.NonPublic | BindingFlags.Instance));

            cursor.EmitDelegate<Action<OuiFileSelectSlot.Button, Vector2, int, int, bool, Wiggler>>((button, position, buttonIndex, i, deleting, wiggler) => {
                if (button == portraitPicker) {
                    // call the portrait picker's Render function to complete the rendering with arrows.
                    portraitPicker.Render(position, buttonIndex == i && !deleting, wiggler.Value * 8f);
                }
            });
        }

        private void onOverworldEnd(On.Celeste.Overworld.orig_End orig, Overworld self) {
            orig(self);

            // just a bit of cleanup.
            portraitPicker = null;
        }

        // very very similar to OuiFileSelectSlotLevelSetPicker from Everest.
        private class PortraitPicker : OuiFileSelectSlot.Button {
            private OuiFileSelectSlot selectSlot;
            private SaveFilePortraitsModule module;

            private Vector2 arrowOffset;
            private int lastDirection;
            private int currentIndex;

            public PortraitPicker(OuiFileSelectSlot selectSlot, SaveFilePortraitsModule module) {
                this.selectSlot = selectSlot;
                this.module = module;

                Label = Dialog.Clean("SaveFilePortraits_ChangePortrait");
                Scale = 0.5f;
                Action = () => changePortraitSelection(1);

                currentIndex = ExistingPortraits.IndexOf(new Tuple<string, string>(module.ModSaveData.Portrait, module.ModSaveData.Animation));
                if (currentIndex == -1) {
                    currentIndex = 0;
                }

                arrowOffset = new Vector2(20f + ActiveFont.Measure(Label).X / 2 * Scale, 0f);
            }

            public void Update(bool selected) {
                if (selected) {
                    if (Input.MenuLeft.Pressed) {
                        changePortraitSelection(-1);
                    } else if (Input.MenuRight.Pressed) {
                        changePortraitSelection(1);
                    }
                } else {
                    lastDirection = 0;
                }
            }

            public void Render(Vector2 position, bool currentlySelected, float wigglerOffset) {
                Vector2 wigglerShift = Vector2.UnitX * (currentlySelected ? wigglerOffset : 0f);
                Color color = selectSlot.SelectionColor(currentlySelected);

                Vector2 leftArrowWigglerShift = lastDirection <= 0 ? wigglerShift : Vector2.Zero;
                Vector2 rightArrowWigglerShift = lastDirection >= 0 ? wigglerShift : Vector2.Zero;

                ActiveFont.DrawOutline("<", position + leftArrowWigglerShift - arrowOffset, new Vector2(0.5f, 0f), Vector2.One * Scale, color, 2f, Color.Black);
                ActiveFont.DrawOutline(">", position + rightArrowWigglerShift + arrowOffset, new Vector2(0.5f, 0f), Vector2.One * Scale, color, 2f, Color.Black);
            }

            private void changePortraitSelection(int direction) {
                lastDirection = direction;
                Audio.Play((direction > 0) ? "event:/ui/main/button_toggle_on" : "event:/ui/main/button_toggle_off");

                currentIndex += direction;

                // handle overflow
                if (currentIndex >= ExistingPortraits.Count)
                    currentIndex = 0;
                if (currentIndex < 0)
                    currentIndex = ExistingPortraits.Count - 1;

                // commit the change to save data
                module.ModSaveData.Portrait = ExistingPortraits[currentIndex].Item1;
                module.ModSaveData.Animation = ExistingPortraits[currentIndex].Item2;

                // apply the change live
                GFX.PortraitsSpriteBank.CreateOn(selectSlot.Portrait, module.ModSaveData.Portrait);
                selectSlot.Portrait.Play(module.ModSaveData.Animation);
                selectSlot.Portrait.Scale = Vector2.One * (200f / GFX.PortraitsSpriteBank.SpriteData[module.ModSaveData.Portrait].Sources[0].XML.AttrInt("size", 160));

                // save the change to disk if the file already exists (if we are not creating one)
                if (selectSlot.Exists) {
                    module.SaveSaveData(selectSlot.FileSlot);
                }

                selectSlot.WiggleMenu();
            }
        }
    }
}