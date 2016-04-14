using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using JuliusSweetland.OptiKey.Enums;
using JuliusSweetland.OptiKey.Extensions;
using JuliusSweetland.OptiKey.Models;
using JuliusSweetland.OptiKey.Properties;
using JuliusSweetland.OptiKey.UI.ViewModels.Keyboards;
using JuliusSweetland.OptiKey.UI.ViewModels.Keyboards.Base;
using Size = JuliusSweetland.OptiKey.UI.ViewModels.Keyboards.SizeAndPosition;

namespace JuliusSweetland.OptiKey.UI.ViewModels
{
    public partial class MainViewModel
    {
        public void AttachServiceEventHandlers()
        {
            Log.Info("AttachServiceEventHandlers called.");

            if (errorNotifyingServices != null)
            {
                errorNotifyingServices.ForEach(s => s.Error += HandleServiceError);
            }

            inputService.PointsPerSecond += (o, value) => { PointsPerSecond = value; };

            inputService.CurrentPosition += (o, tuple) =>
            {
                CurrentPositionPoint = tuple.Item1;
                CurrentPositionKey = tuple.Item2;

                if (keyStateService.KeyDownStates[KeyValues.MouseMagneticCursorKey].Value.IsDownOrLockedDown()
                    && !keyStateService.KeyDownStates[KeyValues.SleepKey].Value.IsDownOrLockedDown() &&
                    !mainWindowManipulationService.IsPointInAppBar(CurrentPositionPoint))
                {
                    mouseOutputService.MoveTo(CurrentPositionPoint);                    
                }
            };

            inputService.SelectionProgress += (o, progress) =>
            {
                if (progress.Item1 == null
                    && progress.Item2 == 0)
                {
                    ResetSelectionProgress(); //Reset all keys
                }
                else if (progress.Item1 != null)
                {
                    if (SelectionMode == SelectionModes.Key
                        && progress.Item1.Value.KeyValue != null)
                    {
                        keyStateService.KeySelectionProgress[progress.Item1.Value.KeyValue.Value] =
                            new NotifyingProxy<double>(progress.Item2);
                    }
                    else if (SelectionMode == SelectionModes.Point)
                    {
                        PointSelectionProgress = new Tuple<Point, double>(progress.Item1.Value.Point, progress.Item2);
                    }
                }
            };

            inputService.Selection += (o, value) =>
            {
                Log.Info("Selection event received from InputService.");

                SelectionResultPoints = null; //Clear captured points from previous SelectionResult event

                if (SelectionMode == SelectionModes.Key
                    && value.KeyValue != null)
                {
                    if (!capturingStateManager.CapturingMultiKeySelection)
                    {
                        audioService.PlaySound(Settings.Default.KeySelectionSoundFile, Settings.Default.KeySelectionSoundVolume);
                    }

                    if (KeySelection != null)
                    {
                        Log.InfoFormat("Firing KeySelection event with KeyValue '{0}'", value.KeyValue.Value);
                        KeySelection(this, value.KeyValue.Value);
                    }
                }
                else if (SelectionMode == SelectionModes.Point)
                {
                    if (PointSelection != null)
                    {
                        PointSelection(this, value.Point);

                        if (nextPointSelectionAction != null)
                        {
                            Log.InfoFormat("Executing nextPointSelectionAction delegate with point '{0}'", value.Point);
                            nextPointSelectionAction(value.Point);
                        }
                    }
                }
            };

            inputService.SelectionResult += (o, tuple) =>
            {
                Log.Info("SelectionResult event received from InputService.");

                var points = tuple.Item1;
                var singleKeyValue = tuple.Item2 != null || tuple.Item3 != null
                    ? new KeyValue { FunctionKey = tuple.Item2, String = tuple.Item3 }
                    : (KeyValue?)null;
                var multiKeySelection = tuple.Item4;

                SelectionResultPoints = points; //Store captured points from SelectionResult event (displayed for debugging)

                if (SelectionMode == SelectionModes.Key
                    && (singleKeyValue != null || (multiKeySelection != null && multiKeySelection.Any())))
                {
                    KeySelectionResult(singleKeyValue, multiKeySelection);
                }
                else if (SelectionMode == SelectionModes.Point)
                {
                    //SelectionResult event has no real meaning when dealing with point selection
                }
            };

            inputService.PointToKeyValueMap = pointToKeyValueMap;
            inputService.SelectionMode = SelectionMode;
        }
        
        private void KeySelectionResult(KeyValue? singleKeyValue, List<string> multiKeySelection)
        {
            //Single key string
            if (singleKeyValue != null
                && !string.IsNullOrEmpty(singleKeyValue.Value.String))
            {
                Log.InfoFormat("KeySelectionResult received with string value '{0}'", singleKeyValue.Value.String.ConvertEscapedCharsToLiterals());
                keyboardOutputService.ProcessSingleKeyText(singleKeyValue.Value.String);
            }

            //Single key function key
            if (singleKeyValue != null
                && singleKeyValue.Value.FunctionKey != null)
            {
                Log.InfoFormat("KeySelectionResult received with function key value '{0}'", singleKeyValue.Value.FunctionKey);
                HandleFunctionKeySelectionResult(singleKeyValue.Value);
            }

            //Multi key selection
            if (multiKeySelection != null
                && multiKeySelection.Any())
            {
                Log.InfoFormat("KeySelectionResult received with '{0}' multiKeySelection results", multiKeySelection.Count);
                keyboardOutputService.ProcessMultiKeyTextAndSuggestions(multiKeySelection);
            }
        }

        // orientation = 0 for due north, and +1 for every 45 degrees clockwise
        private void HandleMinecraftManualLook(int orientation)
        {
            for (int j = 0; j < Settings.Default.MinecraftLookAmount; j++)
            {
                for (int i = 0; i < orientation; ++i)
                {
                    keyboardOutputService.ProcessFunctionKey(FunctionKeys.LeftShift);
                    keyboardOutputService.ProcessSingleKeyText("u");
                }
                keyboardOutputService.ProcessSingleKeyText("i");
            }
        }

        // orientation = 0 for due north, and +1 for every 45 degrees clockwise
        private void HandleMinecraftManualMove(int orientation)
        {
            for (int j = 0; j < Settings.Default.MinecraftMoveAmount; j++)
            {
                for (int i = 0; i < orientation; ++i)
                {
                    keyboardOutputService.ProcessSingleKeyText("o");
                }
                keyboardOutputService.ProcessSingleKeyText("p");
            }
        }

        private void HandleFunctionKeySelectionResult(KeyValue singleKeyValue)
        {
            if (singleKeyValue.FunctionKey != null)
            {
                keyStateService.ProgressKeyDownState(singleKeyValue);

                var currentKeyboard = Keyboard;

                switch (singleKeyValue.FunctionKey.Value)
                {
                    case FunctionKeys.AddToDictionary:
                        AddTextToDictionary();
                        break;

                    case FunctionKeys.AlphaKeyboard:
                        Log.Info("Changing keyboard to Alpha.");
                        Keyboard = new Alpha();
                        break;

                    case FunctionKeys.BackFromKeyboard:
                        Log.Info("Navigating back from keyboard.");
                        var navigableKeyboard = Keyboard as IBackAction;
                        if (navigableKeyboard != null && navigableKeyboard.BackAction != null)
                        {
                            navigableKeyboard.BackAction();
                        }
                        else
                        {
                            Keyboard = new Alpha();
                        }
                        break;

                    case FunctionKeys.Calibrate:
                        if (CalibrationService != null)
                        {
                            Log.Info("Calibrate requested.");
                            
                            var question = CalibrationService.CanBeCompletedWithoutManualIntervention
                                ? Resources.CALIBRATION_CONFIRMATION_MESSAGE
                                : Resources.CALIBRATION_REQUIRES_MANUAL_INTERACTION;
                            
                            Keyboard = new YesNoQuestion(
                                question,
                                () =>
                                {
                                    inputService.RequestSuspend();
                                    Keyboard = currentKeyboard;
                                    CalibrateRequest.Raise(new NotificationWithCalibrationResult(), calibrationResult =>
                                    {
                                        if (calibrationResult.Success)
                                        {
                                            audioService.PlaySound(Settings.Default.InfoSoundFile, Settings.Default.InfoSoundVolume);
                                            RaiseToastNotification(Resources.SUCCESS, calibrationResult.Message, NotificationTypes.Normal, () => inputService.RequestResume());
                                        }
                                        else
                                        {
                                            audioService.PlaySound(Settings.Default.ErrorSoundFile, Settings.Default.ErrorSoundVolume);
                                            RaiseToastNotification(Resources.CRASH_TITLE, calibrationResult.Exception != null
                                                    ? calibrationResult.Exception.Message
                                                    : calibrationResult.Message ?? Resources.UNKNOWN_CALIBRATION_ERROR, 
                                                NotificationTypes.Error, 
                                                () => inputService.RequestResume());
                                        }
                                    });
                                },
                                () =>
                                {
                                    Keyboard = currentKeyboard;
                                });
                        }
                        break;

                    case FunctionKeys.CollapseDock:
                        Log.Info("Collapsing dock.");
                        mainWindowManipulationService.ResizeDockToCollapsed();
                        if (Keyboard is ViewModels.Keyboards.Mouse)
                        {
                            Settings.Default.MouseKeyboardDockSize = DockSizes.Collapsed;
                        }
                        break;

                    case FunctionKeys.ConversationAlphaKeyboard:
                        Log.Info("Changing keyboard to ConversationAlpha.");
                        var opacityBeforeConversationAlpha = mainWindowManipulationService.GetOpacity();
                        Action conversationAlphaBackAction =
                            currentKeyboard is ConversationNumericAndSymbols
                                ? ((ConversationNumericAndSymbols)currentKeyboard).BackAction
                                : () => 
                                    {
                                        Log.Info("Restoring window size.");
                                        mainWindowManipulationService.Restore();
                                        Log.InfoFormat("Restoring window opacity to {0}", opacityBeforeConversationAlpha);
                                        mainWindowManipulationService.SetOpacity(opacityBeforeConversationAlpha);
                                        Keyboard = currentKeyboard;
                                    };
                        Keyboard = new ConversationAlpha(conversationAlphaBackAction);
                        Log.Info("Maximising window.");
                        mainWindowManipulationService.Maximise();
                        Log.InfoFormat("Setting opacity to 1 (fully opaque)");
                        mainWindowManipulationService.SetOpacity(1);
                        break;

                    case FunctionKeys.ConversationNumericAndSymbolsKeyboard:
                        Log.Info("Changing keyboard to ConversationNumericAndSymbols.");
                        var opacityBeforeConversationNumericAndSymbols = mainWindowManipulationService.GetOpacity();
                        Action conversationNumericAndSymbolsBackAction =
                            currentKeyboard is ConversationAlpha
                                ? ((ConversationAlpha)currentKeyboard).BackAction
                                : () => 
                                    {
                                        Log.Info("Restoring window size.");
                                        mainWindowManipulationService.Restore();
                                        Log.InfoFormat("Restoring window opacity to {0}", opacityBeforeConversationNumericAndSymbols);
                                        mainWindowManipulationService.SetOpacity(opacityBeforeConversationNumericAndSymbols);
                                        Keyboard = currentKeyboard;
                                    };
                        Keyboard = new ConversationNumericAndSymbols(conversationNumericAndSymbolsBackAction);
                        Log.Info("Maximising window.");
                        mainWindowManipulationService.Maximise();
                        Log.InfoFormat("Setting opacity to 1 (fully opaque)");
                        mainWindowManipulationService.SetOpacity(1);
                        break;

                    case FunctionKeys.Currencies1Keyboard:
                        Log.Info("Changing keyboard to Currencies1.");
                        Keyboard = new Currencies1();
                        break;

                    case FunctionKeys.Currencies2Keyboard:
                        Log.Info("Changing keyboard to Currencies2.");
                        Keyboard = new Currencies2();
                        break;

                    case FunctionKeys.DecreaseOpacity:
                        Log.Info("Decreasing opacity.");
                        mainWindowManipulationService.IncrementOrDecrementOpacity(false);
                        break;

                    case FunctionKeys.Diacritic1Keyboard:
                        Log.Info("Changing keyboard to Diacritic1.");
                        Keyboard = new Diacritics1();
                        break;

                    case FunctionKeys.Diacritic2Keyboard:
                        Log.Info("Changing keyboard to Diacritic2.");
                        Keyboard = new Diacritics2();
                        break;

                    case FunctionKeys.Diacritic3Keyboard:
                        Log.Info("Changing keyboard to Diacritic3.");
                        Keyboard = new Diacritics3();
                        break;

                    case FunctionKeys.EnglishCanada:
                        Log.Info("Changing keyboard language to EnglishCanada.");
                        Settings.Default.KeyboardAndDictionaryLanguage = Languages.EnglishCanada;
                        Log.Info("Changing keyboard to Menu.");
                        Keyboard = new Menu(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.EnglishUK:
                        Log.Info("Changing keyboard language to EnglishUK.");
                        Settings.Default.KeyboardAndDictionaryLanguage = Languages.EnglishUK;
                        Log.Info("Changing keyboard to Menu.");
                        Keyboard = new Menu(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.EnglishUS:
                        Log.Info("Changing keyboard language to EnglishUS.");
                        Settings.Default.KeyboardAndDictionaryLanguage = Languages.EnglishUS;
                        Log.Info("Changing keyboard to Menu.");
                        Keyboard = new Menu(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.ExpandDock:
                        Log.Info("Expanding dock.");
                        mainWindowManipulationService.ResizeDockToFull();
                        if (Keyboard is ViewModels.Keyboards.Mouse)
                        {
                            Settings.Default.MouseKeyboardDockSize = DockSizes.Full;
                        }
                        break;

                    case FunctionKeys.ExpandToBottom:
                        Log.InfoFormat("Expanding to bottom by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.Bottom, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToBottomAndLeft:
                        Log.InfoFormat("Expanding to bottom and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.BottomLeft, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToBottomAndRight:
                        Log.InfoFormat("Expanding to bottom and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.BottomRight, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToLeft:
                        Log.InfoFormat("Expanding to left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.Left, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToRight:
                        Log.InfoFormat("Expanding to right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.Right, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToTop:
                        Log.InfoFormat("Expanding to top by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.Top, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToTopAndLeft:
                        Log.InfoFormat("Expanding to top and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.TopLeft, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ExpandToTopAndRight:
                        Log.InfoFormat("Expanding to top and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Expand(ExpandToDirections.TopRight, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.FrenchFrance:
                        Log.Info("Changing keyboard language to FrenchFrance.");
                        Settings.Default.KeyboardAndDictionaryLanguage = Languages.FrenchFrance;
                        Log.Info("Changing keyboard to Menu.");
                        Keyboard = new Menu(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.GermanGermany:
                        Log.Info("Changing keyboard language to GermanGermany.");
                        Settings.Default.KeyboardAndDictionaryLanguage = Languages.GermanGermany;
                        Log.Info("Changing keyboard to Menu.");
                        Keyboard = new Menu(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.IncreaseOpacity:
                        Log.Info("Increasing opacity.");
                        mainWindowManipulationService.IncrementOrDecrementOpacity(true);
                        break;

                    case FunctionKeys.Language:
                        Log.Info("Restoring window size.");
                        mainWindowManipulationService.Restore();
                        Log.Info("Changing keyboard to Language.");
                        Keyboard = new Language(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.MenuKeyboard:
                        Log.Info("Restoring window size.");
                        mainWindowManipulationService.Restore();
                        Log.Info("Changing keyboard to Menu.");
                        Keyboard = new Menu(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.MinecraftKeyboard0:
                        Log.Info("Changing keyboard to MinecraftKeyboard.");
                        
                        // Also turn off any modifier keys.
                        Action backActionMC;

                        var lastMagnifierValueMC = keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value;
                        var lastLeftShiftValueMC = keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value;
                        var lastLeftCtrlValueMC = keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value;
                        var lastLeftWinValueMC = keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value;
                        var lastLeftAltValueMC = keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value;
                        var lastScrollSetting = Settings.Default.MouseScrollAmountInClicks;
                        
                        backActionMC = () =>
                        {
                            keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = lastLeftShiftValueMC;
                            keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = lastLeftCtrlValueMC;
                            keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = lastLeftWinValueMC;
                            keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value = lastLeftAltValueMC;
                            keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = lastMagnifierValueMC;

                            Settings.Default.MouseScrollAmountInClicks = lastScrollSetting;    
                            Keyboard = currentKeyboard;

                            // Clear the keyboard when leaving minecraft keyboard.
                            keyboardOutputService.ProcessFunctionKey(FunctionKeys.ClearScratchpad);

                        };
                        
                        Keyboard = new Minecraft(backActionMC);

                        // Default to MinecraftLookMode
                        keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.LockedDown;

                        // Set everything else appropriately
                        keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = KeyDownStates.LockedDown;
                        Settings.Default.MouseScrollAmountInClicks = 1;

                        break;

                    case FunctionKeys.MinecraftKeyboard1:
                        Log.Info("Changing keyboard to MinecraftKeyboard.");

                        // Also turn off any modifier keys.
                        Action backActionMC1;

                        var lastMagnifierValueMC1 = keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value;
                        var lastLeftShiftValueMC1 = keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value;
                        var lastLeftCtrlValueMC1 = keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value;
                        var lastLeftWinValueMC1 = keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value;
                        var lastLeftAltValueMC1 = keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value;
                        var lastScrollSetting1 = Settings.Default.MouseScrollAmountInClicks;
                        var lastMagneticCursorKeyValue1 = keyStateService.KeyDownStates[KeyValues.MouseMagneticCursorKey].Value;

                        backActionMC1 = () =>
                        {
                            keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = lastLeftShiftValueMC1;
                            keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = lastLeftCtrlValueMC1;
                            keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = lastLeftWinValueMC1;
                            keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value = lastLeftAltValueMC1;
                            keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = lastMagnifierValueMC1;

                            Settings.Default.MouseScrollAmountInClicks = lastScrollSetting1;
                            Keyboard = currentKeyboard;

                            // Clear the keyboard when leaving minecraft keyboard.
                            keyboardOutputService.ProcessFunctionKey(FunctionKeys.ClearScratchpad);

                        };

                        Keyboard = new Minecraft1(backActionMC1);

                        // Default to MinecraftLookMode
                        keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.LockedDown;

                        // Set everything else appropriately
                        keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = KeyDownStates.LockedDown;
                        Settings.Default.MouseScrollAmountInClicks = 1;

                        break;

                    case FunctionKeys.MinecraftKeyboard2:
                        Log.Info("Changing keyboard to MinecraftKeyboard.");

                        // Also turn off any modifier keys.
                        Action backActionMC2;

                        var lastMagnifierValueMC2 = keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value;
                        var lastLeftShiftValueMC2 = keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value;
                        var lastLeftCtrlValueMC2 = keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value;
                        var lastLeftWinValueMC2 = keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value;
                        var lastLeftAltValueMC2 = keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value;
                        var lastScrollSetting2 = Settings.Default.MouseScrollAmountInClicks;
                        var lastMagneticCursorKeyValue2 = keyStateService.KeyDownStates[KeyValues.MouseMagneticCursorKey].Value;

                        backActionMC2 = () =>
                        {
                            keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = lastLeftShiftValueMC2;
                            keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = lastLeftCtrlValueMC2;
                            keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = lastLeftWinValueMC2;
                            keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value = lastLeftAltValueMC2;
                            keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = lastMagnifierValueMC2;

                            Settings.Default.MouseScrollAmountInClicks = lastScrollSetting2;
                            Keyboard = currentKeyboard;

                            // Clear the keyboard when leaving minecraft keyboard.
                            keyboardOutputService.ProcessFunctionKey(FunctionKeys.ClearScratchpad);

                        };

                        Keyboard = new Minecraft2(backActionMC2);

                        // Default to MinecraftLookMode
                        keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.LockedDown;

                        // Set everything else appropriately
                        keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = KeyDownStates.LockedDown;
                        Settings.Default.MouseScrollAmountInClicks = 1;

                        break;

                    case FunctionKeys.MinecraftInventoryKeyboard:
                        Log.Info("Changing keyboard to MinecraftInventoryKeyboard.");

                        // Default to MinecraftLookMode, unless already in MinecraftMoveMode
                        // Also turn off any modifier keys.
                        Action backActionMCInventory;

                        var lastMagnifierValue = keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value;
                        var lastLeftShiftValueMCI = keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value;
                        var lastLeftCtrlValueMCI = keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value;
                        var lastLeftWinValueMCI = keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value;
                        var lastLeftAltValueMCI = keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value;
                        var lastMagneticCursorKeyValue = keyStateService.KeyDownStates[KeyValues.MouseMagneticCursorKey].Value;

                        backActionMCInventory = () =>
                        {
                            Keyboard = currentKeyboard;
                            keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = lastMagnifierValue;

                            keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = lastLeftShiftValueMCI;
                            keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = lastLeftCtrlValueMCI;
                            keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = lastLeftWinValueMCI;
                            keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value = lastLeftAltValueMCI;
                            keyStateService.KeyDownStates[KeyValues.MouseMagneticCursorKey].Value = lastMagneticCursorKeyValue;

                            // Clear the keyboard when leaving minecraft keyboard.
                            keyboardOutputService.ProcessFunctionKey(FunctionKeys.ClearScratchpad);

                        };

                        keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = KeyDownStates.LockedDown;
                        Keyboard = new MinecraftSurvivalInventory(backActionMCInventory);

                        // Set everything else appropriately
                        keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = KeyDownStates.Up;
                        keyStateService.KeyDownStates[KeyValues.MouseMagneticCursorKey].Value = KeyDownStates.Up;

                        Settings.Default.MouseScrollAmountInClicks = 1;

                        break;

                    // Look mode and Move mode are mutually exclusive.
                    case FunctionKeys.MinecraftLookMode:
                        if (keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value.IsDownOrLockedDown())
                        {
                            keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.Up;
                        }
                        else if (!keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value.IsDownOrLockedDown())
                        {
                            keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value = KeyDownStates.LockedDown;
                        }
                        break;

                    case FunctionKeys.MinecraftMoveMode:
                        if (keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value.IsDownOrLockedDown())
                        {
                            keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.Up;
                        }
                        else if (!keyStateService.KeyDownStates[KeyValues.MinecraftMoveModeKey].Value.IsDownOrLockedDown())
                        {
                            keyStateService.KeyDownStates[KeyValues.MinecraftLookModeKey].Value = KeyDownStates.LockedDown;
                        }
                        break;
                    case FunctionKeys.Minimise:
                        Log.Info("Minimising window.");
                        mainWindowManipulationService.Minimise();
                        Log.Info("Changing keyboard to Minimised.");
                        Keyboard = new Minimised(() =>
                        {
                            Log.Info("Restoring window size.");
                            mainWindowManipulationService.Restore();
                            Keyboard = currentKeyboard;
                        });
                        break;

                    case FunctionKeys.MouseDrag:
                        Log.Info("Mouse drag selected.");
                        SetupFinalClickAction(firstFinalPoint =>
                        {
                            if (firstFinalPoint != null)
                            {
                                audioService.PlaySound(Settings.Default.MouseDownSoundFile, Settings.Default.MouseDownSoundVolume);
                                
                                //This class reacts to the point selection event AFTER the MagnifyPopup reacts to it.
                                //This means that if the MagnifyPopup sets the nextPointSelectionAction from the
                                //MagnifiedPointSelectionAction then it will be called immediately i.e. for the same point.
                                //The workaround is to set the nextPointSelectionAction to a lambda which sets the NEXT
                                //nextPointSelectionAction. This means the immediate call to the lambda just sets up the
                                //delegate for the subsequent call.
                                nextPointSelectionAction = repeatFirstClickOrSecondClickAction =>
                                {
                                    Action<Point> deferIfMagnifyingElseDoNow = repeatFirstClickOrSecondClickPoint =>
                                    {
                                        Action<Point?> secondFinalClickAction = secondFinalPoint =>
                                        {
                                            if (secondFinalPoint != null)
                                            {
                                                Action<Point, Point> simulateDrag = (fp1, fp2) =>
                                                {
                                                    Log.InfoFormat("Performing mouse drag between points ({0},{1}) and {2},{3}).", fp1.X, fp1.Y, fp2.X, fp2.Y);
                                                    mouseOutputService.MoveTo(fp1);
                                                    mouseOutputService.LeftButtonDown();
                                                    audioService.PlaySound(Settings.Default.MouseUpSoundFile, Settings.Default.MouseUpSoundVolume);
                                                    mouseOutputService.MoveTo(fp2);
                                                    mouseOutputService.LeftButtonUp();
                                                };

                                                lastMouseActionStateManager.LastMouseAction =
                                                    () => simulateDrag(firstFinalPoint.Value, secondFinalPoint.Value);
                                                simulateDrag(firstFinalPoint.Value, secondFinalPoint.Value);
                                            }

                                            ResetAndCleanupAfterMouseAction();
                                        };

                                        if (keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value.IsDownOrLockedDown())
                                        {
                                            ShowCursor = false; //See MouseMoveAndLeftClick case for explanation of this
                                            MagnifiedPointSelectionAction = secondFinalClickAction;
                                            MagnifyAtPoint = repeatFirstClickOrSecondClickPoint;
                                            ShowCursor = true;
                                        }
                                        else
                                        {
                                            secondFinalClickAction(repeatFirstClickOrSecondClickPoint);
                                        }

                                        nextPointSelectionAction = null;
                                    };

                                    if (keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value.IsDownOrLockedDown())
                                    {
                                        nextPointSelectionAction = deferIfMagnifyingElseDoNow;
                                    }
                                    else
                                    {
                                        deferIfMagnifyingElseDoNow(repeatFirstClickOrSecondClickAction);
                                    }
                                };
                            }
                            else
                            {
                                //Reset and clean up if we are not continuing to 2nd point
                                SelectionMode = SelectionModes.Key;
                                nextPointSelectionAction = null;
                                ShowCursor = false;
                                if (keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value == KeyDownStates.Down)
                                {
                                    keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = KeyDownStates.Up; //Release magnifier if down but not locked down
                                }
                            }

                            //Reset and clean up
                            MagnifyAtPoint = null;
                            MagnifiedPointSelectionAction = null;
                        }, finalClickInSeries: false);
                        break;

                    case FunctionKeys.MouseKeyboard:
                        Log.Info("Changing keyboard to Mouse.");
                        Action backAction;
                        if (keyStateService.SimulateKeyStrokes
                            && Settings.Default.SuppressModifierKeysWhenInMouseKeyboard)
                        {
                            var lastLeftShiftValue = keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value;
                            var lastLeftCtrlValue = keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value;
                            var lastLeftWinValue = keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value;
                            var lastLeftAltValue = keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value;
                            keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = KeyDownStates.Up;
                            keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value = KeyDownStates.Up;
                            backAction = () =>
                            {
                                keyStateService.KeyDownStates[KeyValues.LeftShiftKey].Value = lastLeftShiftValue;
                                keyStateService.KeyDownStates[KeyValues.LeftCtrlKey].Value = lastLeftCtrlValue;
                                keyStateService.KeyDownStates[KeyValues.LeftWinKey].Value = lastLeftWinValue;
                                keyStateService.KeyDownStates[KeyValues.LeftAltKey].Value = lastLeftAltValue;
                                Keyboard = currentKeyboard;
                            };
                        }
                        else
                        {
                            backAction = () => Keyboard = currentKeyboard;
                        }
                        Keyboard = new Mouse(backAction);
                        //Reinstate mouse keyboard docked state (if docked)
                        if (Settings.Default.MainWindowState == WindowStates.Docked)
                        {
                            if (Settings.Default.MouseKeyboardDockSize == DockSizes.Full
                                && Settings.Default.MainWindowDockSize != DockSizes.Full)
                            {
                                mainWindowManipulationService.ResizeDockToFull();
                            }
                            else if (Settings.Default.MouseKeyboardDockSize == DockSizes.Collapsed
                                && Settings.Default.MainWindowDockSize != DockSizes.Collapsed)
                            {
                                mainWindowManipulationService.ResizeDockToCollapsed();
                            }
                        }
                        break;

                    case FunctionKeys.MouseLeftClick:
                        var leftClickPoint = mouseOutputService.GetCursorPosition();
                        Log.InfoFormat("Mouse left click selected at point ({0},{1}).", leftClickPoint.X, leftClickPoint.Y);
                        Action<Point?> performLeftClick = point =>
                        {
                            if (point != null)
                            {
                                mouseOutputService.MoveTo(point.Value);
                            }
                            audioService.PlaySound(Settings.Default.MouseClickSoundFile, Settings.Default.MouseClickSoundVolume);
                            mouseOutputService.LeftButtonClick();
                        };
                        lastMouseActionStateManager.LastMouseAction = () => performLeftClick(leftClickPoint);
                        performLeftClick(null);
                        break;

                    case FunctionKeys.MouseLeftDoubleClick:
                        var leftDoubleClickPoint = mouseOutputService.GetCursorPosition();
                        Log.InfoFormat("Mouse left double click selected at point ({0},{1}).", leftDoubleClickPoint.X, leftDoubleClickPoint.Y);
                        Action<Point?> performLeftDoubleClick = point =>
                        {
                            if (point != null)
                            {
                                mouseOutputService.MoveTo(point.Value);
                            }
                            audioService.PlaySound(Settings.Default.MouseDoubleClickSoundFile, Settings.Default.MouseDoubleClickSoundVolume);
                            mouseOutputService.LeftButtonDoubleClick();
                        };
                        lastMouseActionStateManager.LastMouseAction = () => performLeftDoubleClick(leftDoubleClickPoint);
                        performLeftDoubleClick(null);
                        break;

                    case FunctionKeys.MouseLeftDownUp:
                        var leftDownUpPoint = mouseOutputService.GetCursorPosition();
                        if (keyStateService.KeyDownStates[KeyValues.MouseLeftDownUpKey].Value.IsDownOrLockedDown())
                        {
                            Log.InfoFormat("Pressing mouse left button down at point ({0},{1}).", leftDownUpPoint.X, leftDownUpPoint.Y);
                            audioService.PlaySound(Settings.Default.MouseDownSoundFile, Settings.Default.MouseDownSoundVolume);
                            mouseOutputService.LeftButtonDown();
                            lastMouseActionStateManager.LastMouseAction = null;
                        }
                        else
                        {
                            Log.InfoFormat("Releasing mouse left button at point ({0},{1}).", leftDownUpPoint.X, leftDownUpPoint.Y);
                            audioService.PlaySound(Settings.Default.MouseUpSoundFile, Settings.Default.MouseUpSoundVolume);
                            mouseOutputService.LeftButtonUp();
                            lastMouseActionStateManager.LastMouseAction = null;
                        }
                        break;

                    case FunctionKeys.MouseMiddleClick:
                        var middleClickPoint = mouseOutputService.GetCursorPosition();
                        Log.InfoFormat("Mouse middle click selected at point ({0},{1}).", middleClickPoint.X, middleClickPoint.Y);
                        Action<Point?> performMiddleClick = point =>
                        {
                            if (point != null)
                            {
                                mouseOutputService.MoveTo(point.Value);
                            }
                            audioService.PlaySound(Settings.Default.MouseClickSoundFile, Settings.Default.MouseClickSoundVolume);
                            mouseOutputService.MiddleButtonClick();
                        };
                        lastMouseActionStateManager.LastMouseAction = () => performMiddleClick(middleClickPoint);
                        performMiddleClick(null);
                        break;

                    case FunctionKeys.MouseMiddleDownUp:
                        var middleDownUpPoint = mouseOutputService.GetCursorPosition();
                        if (keyStateService.KeyDownStates[KeyValues.MouseMiddleDownUpKey].Value.IsDownOrLockedDown())
                        {
                            Log.InfoFormat("Pressing mouse middle button down at point ({0},{1}).", middleDownUpPoint.X, middleDownUpPoint.Y);
                            audioService.PlaySound(Settings.Default.MouseDownSoundFile, Settings.Default.MouseDownSoundVolume);
                            mouseOutputService.MiddleButtonDown();
                            lastMouseActionStateManager.LastMouseAction = null;
                        }
                        else
                        {
                            Log.InfoFormat("Releasing mouse middle button at point ({0},{1}).", middleDownUpPoint.X, middleDownUpPoint.Y);
                            audioService.PlaySound(Settings.Default.MouseUpSoundFile, Settings.Default.MouseUpSoundVolume);
                            mouseOutputService.MiddleButtonUp();
                            lastMouseActionStateManager.LastMouseAction = null;
                        }
                        break;

                    case FunctionKeys.MouseMoveAndLeftClick:
                        Log.Info("Mouse move and left click selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateClick = fp =>
                                {
                                    Log.InfoFormat("Performing mouse left click at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseClickSoundFile, Settings.Default.MouseClickSoundVolume);
                                    mouseOutputService.MoveAndLeftClick(fp, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateClick(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateClick(finalPoint.Value);
                            }

                            ResetAndCleanupAfterMouseAction();
                        });
                        break;

                    case FunctionKeys.MouseMoveAndLeftDoubleClick:
                        Log.Info("Mouse move and left double click selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateClick = fp =>
                                {
                                    Log.InfoFormat("Performing mouse left double click at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseDoubleClickSoundFile, Settings.Default.MouseDoubleClickSoundVolume);
                                    mouseOutputService.MoveAndLeftDoubleClick(fp, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateClick(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateClick(finalPoint.Value);
                            }
                            
                            ResetAndCleanupAfterMouseAction();
                        });
                        break;

                    case FunctionKeys.MouseMoveAndMiddleClick:
                        Log.Info("Mouse move and middle click selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateClick = fp =>
                                {
                                    Log.InfoFormat("Performing mouse middle click at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseClickSoundFile, Settings.Default.MouseClickSoundVolume);
                                    mouseOutputService.MoveAndMiddleClick(fp, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateClick(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateClick(finalPoint.Value);
                            }

                            ResetAndCleanupAfterMouseAction();
                        });
                        break;
                        
                    case FunctionKeys.MouseMoveAndRightClick:
                        Log.Info("Mouse move and right click selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateClick = fp =>
                                {
                                    Log.InfoFormat("Performing mouse right click at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseClickSoundFile, Settings.Default.MouseClickSoundVolume);
                                    mouseOutputService.MoveAndRightClick(fp, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateClick(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateClick(finalPoint.Value);
                            }

                            ResetAndCleanupAfterMouseAction();
                        });
                        break;

                    case FunctionKeys.MouseMoveAmountInPixels:
                        Log.Info("Progressing MouseMoveAmountInPixels.");
                        switch (Settings.Default.MouseMoveAmountInPixels)
                        {
                            case 1:
                                Settings.Default.MouseMoveAmountInPixels = 5;
                                break;

                            case 5:
                                Settings.Default.MouseMoveAmountInPixels = 10;
                                break;

                            case 10:
                                Settings.Default.MouseMoveAmountInPixels = 25;
                                break;

                            case 25:
                                Settings.Default.MouseMoveAmountInPixels = 50;
                                break;

                            case 50:
                                Settings.Default.MouseMoveAmountInPixels = 100;
                                break;

                            default:
                                Settings.Default.MouseMoveAmountInPixels = 1;
                                break;
                        }
                        break;

                    case FunctionKeys.MouseMoveAndScrollToBottom:
                        Log.Info("Mouse move and scroll to bottom selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateScrollToBottom = fp =>
                                {
                                    Log.InfoFormat("Performing mouse scroll to bottom at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseScrollSoundFile, Settings.Default.MouseScrollSoundVolume);
                                    mouseOutputService.MoveAndScrollWheelDown(fp, Settings.Default.MouseScrollAmountInClicks, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateScrollToBottom(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateScrollToBottom(finalPoint.Value);
                            }

                            ResetAndCleanupAfterMouseAction();
                        }, suppressMagnification:true);
                        break;

                    case FunctionKeys.MouseMoveAndScrollToLeft:
                        Log.Info("Mouse move and scroll to left selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateScrollToLeft = fp =>
                                {
                                    Log.InfoFormat("Performing mouse scroll to left at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseScrollSoundFile, Settings.Default.MouseScrollSoundVolume);
                                    mouseOutputService.MoveAndScrollWheelLeft(fp, Settings.Default.MouseScrollAmountInClicks, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateScrollToLeft(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateScrollToLeft(finalPoint.Value);
                            }

                            ResetAndCleanupAfterMouseAction();
                        }, suppressMagnification: true);
                        break;

                    case FunctionKeys.MouseMoveAndScrollToRight:
                        Log.Info("Mouse move and scroll to right selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateScrollToRight = fp =>
                                {
                                    Log.InfoFormat("Performing mouse scroll to right at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseScrollSoundFile, Settings.Default.MouseScrollSoundVolume);
                                    mouseOutputService.MoveAndScrollWheelRight(fp, Settings.Default.MouseScrollAmountInClicks, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateScrollToRight(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateScrollToRight(finalPoint.Value);
                            }

                            ResetAndCleanupAfterMouseAction();
                        }, suppressMagnification: true);
                        break;

                    case FunctionKeys.MouseMoveAndScrollToTop:
                        Log.Info("Mouse move and scroll to top selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateScrollToTop = fp =>
                                {
                                    Log.InfoFormat("Performing mouse scroll to top at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseScrollSoundFile, Settings.Default.MouseScrollSoundVolume);
                                    mouseOutputService.MoveAndScrollWheelUp(fp, Settings.Default.MouseScrollAmountInClicks, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateScrollToTop(finalPoint.Value);
                                ShowCursor = false; //Hide cursor popup before performing action as it is possible for it to be performed on the popup
                                simulateScrollToTop(finalPoint.Value);
                            }

                            ResetAndCleanupAfterMouseAction();
                        }, suppressMagnification: true);
                        break;

                    case FunctionKeys.MouseScrollToTop:

                        var currentPoint = mouseOutputService.GetCursorPosition();
                        Log.InfoFormat("Mouse scroll to top selected at point ({0},{1}).", currentPoint.X, currentPoint.Y);
                        Action<Point?> performScroll = point =>
                        {
                            if (point != null)
                            {
                                Action<Point> simulateScrollToTop = fp =>
                                {
                                    Log.InfoFormat("Performing mouse scroll to top at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseScrollSoundFile, Settings.Default.MouseScrollSoundVolume);
                                    mouseOutputService.MoveAndScrollWheelUp(fp, Settings.Default.MouseScrollAmountInClicks, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateScrollToTop(point.Value);
                                simulateScrollToTop(point.Value);
                            }
                        };
                        performScroll(currentPoint);
                        ResetAndCleanupAfterMouseAction();

                        break;

                    case FunctionKeys.MouseScrollToBottom:

                        var currentPointScroll = mouseOutputService.GetCursorPosition();
                        Log.InfoFormat("Mouse scroll to top selected at point ({0},{1}).", currentPointScroll.X, currentPointScroll.Y);
                        Action<Point?> performScrollDown = point =>
                        {
                            if (point != null)
                            {
                                Action<Point> simulateScrollToBottom = fp =>
                                {
                                    Log.InfoFormat("Performing mouse scroll to top at point ({0},{1}).", fp.X, fp.Y);
                                    audioService.PlaySound(Settings.Default.MouseScrollSoundFile, Settings.Default.MouseScrollSoundVolume);
                                    mouseOutputService.MoveAndScrollWheelDown(fp, Settings.Default.MouseScrollAmountInClicks, true);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateScrollToBottom(point.Value);
                                simulateScrollToBottom(point.Value);
                            }
                        };
                        performScrollDown(currentPointScroll);
                        ResetAndCleanupAfterMouseAction();

                        break;

                    case FunctionKeys.MouseMoveTo:
                        Log.Info("Mouse move to selected.");
                        SetupFinalClickAction(finalPoint =>
                        {
                            if (finalPoint != null)
                            {
                                Action<Point> simulateMoveTo = fp =>
                                {
                                    Log.InfoFormat("Performing mouse move to point ({0},{1}).", fp.X, fp.Y);
                                    mouseOutputService.MoveTo(fp);
                                };
                                lastMouseActionStateManager.LastMouseAction = () => simulateMoveTo(finalPoint.Value);
                                simulateMoveTo(finalPoint.Value);
                            }
                            ResetAndCleanupAfterMouseAction();
                        });
                        break;

                    case FunctionKeys.MouseMoveToBottom:
                        Log.Info("Mouse move to bottom selected.");
                        Action simulateMoveToBottom = () =>
                        {
                            var cursorPosition = mouseOutputService.GetCursorPosition();
                            var moveToPoint = new Point(cursorPosition.X, cursorPosition.Y + Settings.Default.MouseMoveAmountInPixels);
                            Log.InfoFormat("Performing mouse move to point ({0},{1}).", moveToPoint.X, moveToPoint.Y);
                            mouseOutputService.MoveTo(moveToPoint);
                        };
                        lastMouseActionStateManager.LastMouseAction = simulateMoveToBottom;
                        simulateMoveToBottom();
                        break;

                    case FunctionKeys.MouseMoveToLeft:
                        Log.Info("Mouse move to left selected.");
                        Action simulateMoveToLeft = () =>
                        {
                            var cursorPosition = mouseOutputService.GetCursorPosition();
                            var moveToPoint = new Point(cursorPosition.X - Settings.Default.MouseMoveAmountInPixels, cursorPosition.Y);
                            Log.InfoFormat("Performing mouse move to point ({0},{1}).", moveToPoint.X, moveToPoint.Y);
                            mouseOutputService.MoveTo(moveToPoint);
                        };
                        lastMouseActionStateManager.LastMouseAction = simulateMoveToLeft;
                        simulateMoveToLeft();
                        break;

                    case FunctionKeys.MouseMoveToRight:
                        Log.Info("Mouse move to right selected.");
                        Action simulateMoveToRight = () =>
                        {
                            var cursorPosition = mouseOutputService.GetCursorPosition();
                            var moveToPoint = new Point(cursorPosition.X + Settings.Default.MouseMoveAmountInPixels, cursorPosition.Y);
                            Log.InfoFormat("Performing mouse move to point ({0},{1}).", moveToPoint.X, moveToPoint.Y);
                            mouseOutputService.MoveTo(moveToPoint);
                        };
                        lastMouseActionStateManager.LastMouseAction = simulateMoveToRight;
                        simulateMoveToRight();
                        break;

                    case FunctionKeys.MouseMoveToTop:
                        Log.Info("Mouse move to top selected.");
                        Action simulateMoveToTop = () =>
                        {
                            var cursorPosition = mouseOutputService.GetCursorPosition();
                            var moveToPoint = new Point(cursorPosition.X, cursorPosition.Y - Settings.Default.MouseMoveAmountInPixels);
                            Log.InfoFormat("Performing mouse move to point ({0},{1}).", moveToPoint.X, moveToPoint.Y);
                            mouseOutputService.MoveTo(moveToPoint);
                        };
                        lastMouseActionStateManager.LastMouseAction = simulateMoveToTop;
                        simulateMoveToTop();
                        break;

         

                    case FunctionKeys.MinecraftLookNorth:
                        Log.Info("Minecraft move/look to north selected.");
                        HandleMinecraftManualLook(0);
                        break;

                    case FunctionKeys.MinecraftLookNorthEast:
                        Log.Info("Minecraft move/look to north-east selected.");
                        HandleMinecraftManualLook(1);
                        break;

                    case FunctionKeys.MinecraftLookEast:
                        Log.Info("Minecraft move/look to east selected.");
                        HandleMinecraftManualLook(2);
                        break;

                    case FunctionKeys.MinecraftLookSouthEast:
                        Log.Info("Minecraft move/look to south-east selected.");
                        HandleMinecraftManualLook(3);
                        break;

                    case FunctionKeys.MinecraftLookSouth:
                        Log.Info("Minecraft move/look to south selected.");
                        HandleMinecraftManualLook(4);
                        break;

                    case FunctionKeys.MinecraftLookSouthWest:
                        Log.Info("Minecraft move/look to south-west selected.");
                        HandleMinecraftManualLook(5);
                        break;

                    case FunctionKeys.MinecraftLookWest:
                        Log.Info("Minecraft move/look to west selected.");
                        HandleMinecraftManualLook(6);
                        break;

                    case FunctionKeys.MinecraftLookNorthWest:
                        Log.Info("Minecraft move/look to north-west selected.");
                        HandleMinecraftManualLook(7);
                        break;

                    case FunctionKeys.MinecraftMoveNorth:
                        Log.Info("Minecraft move/look to north selected.");
                        HandleMinecraftManualMove(0);
                        break;

                    case FunctionKeys.MinecraftMoveNorthEast:
                        Log.Info("Minecraft move/look to north-east selected.");
                        HandleMinecraftManualMove(1);
                        break;

                    case FunctionKeys.MinecraftMoveEast:
                        Log.Info("Minecraft move/look to east selected.");
                        HandleMinecraftManualMove(2);
                        break;

                    case FunctionKeys.MinecraftMoveSouthEast:
                        Log.Info("Minecraft move/look to south-east selected.");
                        HandleMinecraftManualMove(3);
                        break;

                    case FunctionKeys.MinecraftMoveSouth:
                        Log.Info("Minecraft move/look to south selected.");
                        HandleMinecraftManualMove(4);
                        break;

                    case FunctionKeys.MinecraftMoveSouthWest:
                        Log.Info("Minecraft move/look to south-west selected.");
                        HandleMinecraftManualMove(5);
                        break;

                    case FunctionKeys.MinecraftMoveWest:
                        Log.Info("Minecraft move/look to west selected.");
                        HandleMinecraftManualMove(6);
                        break;

                    case FunctionKeys.MinecraftMoveNorthWest:
                        Log.Info("Minecraft move/look to north-west selected.");
                        HandleMinecraftManualMove(7);
                        break;

                    case FunctionKeys.MouseRightClick:
                        var rightClickPoint = mouseOutputService.GetCursorPosition();
                        Log.InfoFormat("Mouse right click selected at point ({0},{1}).", rightClickPoint.X, rightClickPoint.Y);
                        Action<Point?> performRightClick = point =>
                        {
                            if (point != null)
                            {
                                mouseOutputService.MoveTo(point.Value);
                            }
                            audioService.PlaySound(Settings.Default.MouseClickSoundFile, Settings.Default.MouseClickSoundVolume);
                            mouseOutputService.RightButtonClick();
                        };
                        lastMouseActionStateManager.LastMouseAction = () => performRightClick(rightClickPoint);
                        performRightClick(null);
                        break;

                    case FunctionKeys.MouseRightDownUp:
                        var rightDownUpPoint = mouseOutputService.GetCursorPosition();
                        if (keyStateService.KeyDownStates[KeyValues.MouseRightDownUpKey].Value.IsDownOrLockedDown())
                        {
                            Log.InfoFormat("Pressing mouse right button down at point ({0},{1}).", rightDownUpPoint.X, rightDownUpPoint.Y);
                            audioService.PlaySound(Settings.Default.MouseDownSoundFile, Settings.Default.MouseDownSoundVolume);
                            mouseOutputService.RightButtonDown();
                            lastMouseActionStateManager.LastMouseAction = null;
                        }
                        else
                        {
                            Log.InfoFormat("Releasing mouse right button at point ({0},{1}).", rightDownUpPoint.X, rightDownUpPoint.Y);
                            audioService.PlaySound(Settings.Default.MouseUpSoundFile, Settings.Default.MouseUpSoundVolume);
                            mouseOutputService.RightButtonUp();
                            lastMouseActionStateManager.LastMouseAction = null;
                        }
                        break;

                    case FunctionKeys.MoveAndResizeAdjustmentAmount:
                        Log.Info("Progressing MoveAndResizeAdjustmentAmount.");
                        switch (Settings.Default.MoveAndResizeAdjustmentAmountInPixels)
                        {
                            case 1:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 5;
                                break;

                            case 5:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 10;
                                break;

                            case 10:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 25;
                                break;

                            case 25:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 50;
                                break;

                            case 50:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 100;
                                break;

                            default:
                                Settings.Default.MoveAndResizeAdjustmentAmountInPixels = 1;
                                break;
                        }
                        break;

                    case FunctionKeys.MouseScrollAmountInClicks:
                        Log.Info("Progressing MouseScrollAmountInClicks.");
                        switch (Settings.Default.MouseScrollAmountInClicks)
                        {
                            case 1:
                                Settings.Default.MouseScrollAmountInClicks = 3;
                                break;

                            case 3:
                                Settings.Default.MouseScrollAmountInClicks = 5;
                                break;

                            case 5:
                                Settings.Default.MouseScrollAmountInClicks = 10;
                                break;

                            case 10:
                                Settings.Default.MouseScrollAmountInClicks = 25;
                                break;

                            default:
                                Settings.Default.MouseScrollAmountInClicks = 1;
                                break;
                        }
                        break;

                    case FunctionKeys.MinecraftMoveAmount:
                        Log.Info("Progressing MinecraftMoveAmount.");
                        switch (Settings.Default.MinecraftMoveAmount)
                        {
                            case 1:
                                Settings.Default.MinecraftMoveAmount = 2;
                                break;

                            case 2:
                                Settings.Default.MinecraftMoveAmount = 4;
                                break;

                            case 4:
                                Settings.Default.MinecraftMoveAmount = 8;
                                break;

                            default:
                                Settings.Default.MinecraftMoveAmount = 1;
                                break;
                        }
                        break;

                    case FunctionKeys.MinecraftLookAmount:
                        Log.Info("Progressing MinecraftLookAmount.");
                        switch (Settings.Default.MinecraftLookAmount)
                        {
                            case 1:
                                Settings.Default.MinecraftLookAmount = 2;
                                break;

                            case 2:
                                Settings.Default.MinecraftLookAmount = 4;
                                break;

                            case 4:
                                Settings.Default.MinecraftLookAmount = 8;
                                break;

                            default:
                                Settings.Default.MinecraftLookAmount = 1;
                                break;
                        }
                        break;

                    case FunctionKeys.MoveToBottom:
                        Log.InfoFormat("Moving to bottom by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.Bottom, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToBottomAndLeft:
                        Log.InfoFormat("Moving to bottom and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.BottomLeft, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToBottomAndLeftBoundaries:
                        Log.Info("Moving to bottom and left boundaries.");
                        mainWindowManipulationService.Move(MoveToDirections.BottomLeft, null);
                        break;

                    case FunctionKeys.MoveToBottomAndRight:
                        Log.InfoFormat("Moving to bottom and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.BottomRight, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToBottomAndRightBoundaries:
                        Log.Info("Moving to bottom and right boundaries.");
                        mainWindowManipulationService.Move(MoveToDirections.BottomRight, null);
                        break;

                    case FunctionKeys.MoveToBottomBoundary:
                        Log.Info("Moving to bottom boundary.");
                        mainWindowManipulationService.Move(MoveToDirections.Bottom, null);
                        break;

                    case FunctionKeys.MoveToLeft:
                        Log.InfoFormat("Moving to left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.Left, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToLeftBoundary:
                        Log.Info("Moving to left boundary.");
                        mainWindowManipulationService.Move(MoveToDirections.Left, null);
                        break;

                    case FunctionKeys.MoveToRight:
                        Log.InfoFormat("Moving to right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.Right, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToRightBoundary:
                        Log.Info("Moving to right boundary.");
                        mainWindowManipulationService.Move(MoveToDirections.Right, null);
                        break;

                    case FunctionKeys.MoveToTop:
                        Log.InfoFormat("Moving to top by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.Top, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToTopAndLeft:
                        Log.InfoFormat("Moving to top and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.TopLeft, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToTopAndLeftBoundaries:
                        Log.Info("Moving to top and left boundaries.");
                        mainWindowManipulationService.Move(MoveToDirections.TopLeft, null);
                        break;

                    case FunctionKeys.MoveToTopAndRight:
                        Log.InfoFormat("Moving to top and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Move(MoveToDirections.TopRight, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.MoveToTopAndRightBoundaries:
                        Log.Info("Moving to top and right boundaries.");
                        mainWindowManipulationService.Move(MoveToDirections.TopRight, null);
                        break;

                    case FunctionKeys.MoveToTopBoundary:
                        Log.Info("Moving to top boundary.");
                        mainWindowManipulationService.Move(MoveToDirections.Top, null);
                        break;
                        
                    case FunctionKeys.NextSuggestions:
                        Log.Info("Incrementing suggestions page.");

                        if (suggestionService.Suggestions != null
                            && (suggestionService.Suggestions.Count > (suggestionService.SuggestionsPage + 1) * SuggestionService.SuggestionsPerPage))
                        {
                            suggestionService.SuggestionsPage++;
                        }
                        break;

                    case FunctionKeys.NoQuestionResult:
                        HandleYesNoQuestionResult(false);
                        break;

                    case FunctionKeys.NumericAndSymbols1Keyboard:
                        Log.Info("Changing keyboard to NumericAndSymbols1.");
                        Keyboard = new NumericAndSymbols1();
                        break;

                    case FunctionKeys.NumericAndSymbols2Keyboard:
                        Log.Info("Changing keyboard to NumericAndSymbols2.");
                        Keyboard = new NumericAndSymbols2();
                        break;

                    case FunctionKeys.NumericAndSymbols3Keyboard:
                        Log.Info("Changing keyboard to Symbols3.");
                        Keyboard = new NumericAndSymbols3();
                        break;

                    case FunctionKeys.PhysicalKeysKeyboard:
                        Log.Info("Changing keyboard to PhysicalKeys.");
                        Keyboard = new PhysicalKeys();
                        break;
                        
                    case FunctionKeys.PreviousSuggestions:
                        Log.Info("Decrementing suggestions page.");

                        if (suggestionService.SuggestionsPage > 0)
                        {
                            suggestionService.SuggestionsPage--;
                        }
                        break;

                    case FunctionKeys.Quit:
                        Log.Info("Quit key selected.");
                        var keyboardBeforeQuit = Keyboard;
                        Keyboard = new YesNoQuestion(Resources.QUIT_MESSAGE,
                            () =>
                            {
                                Keyboard = new YesNoQuestion(Resources.QUIT_CONFIRMATION_MESSAGE,
                                    () => Application.Current.Shutdown(),
                                    () => { Keyboard = keyboardBeforeQuit; });
                            },
                            () => { Keyboard = keyboardBeforeQuit; });
                        break;

                    case FunctionKeys.RepeatLastMouseAction:
                        if (lastMouseActionStateManager.LastMouseAction != null)
                        {
                            lastMouseActionStateManager.LastMouseAction();
                        }
                        break;

                    case FunctionKeys.RussianRussia:
                        Log.Info("Changing keyboard language to RussianRussia.");
                        Settings.Default.KeyboardAndDictionaryLanguage = Languages.RussianRussia;
                        Log.Info("Changing keyboard to Menu.");
                        Keyboard = new Menu(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.ShrinkFromBottom:
                        Log.InfoFormat("Shrinking from bottom by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.Bottom, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromBottomAndLeft:
                        Log.InfoFormat("Shrinking from bottom and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.BottomLeft, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromBottomAndRight:
                        Log.InfoFormat("Shrinking from bottom and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.BottomRight, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromLeft:
                        Log.InfoFormat("Shrinking from left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.Left, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromRight:
                        Log.InfoFormat("Shrinking from right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.Right, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromTop:
                        Log.InfoFormat("Shrinking from top by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.Top, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromTopAndLeft:
                        Log.InfoFormat("Shrinking from top and left by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.TopLeft, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.ShrinkFromTopAndRight:
                        Log.InfoFormat("Shrinking from top and right by {0}px.", Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        mainWindowManipulationService.Shrink(ShrinkFromDirections.TopRight, Settings.Default.MoveAndResizeAdjustmentAmountInPixels);
                        break;

                    case FunctionKeys.SizeAndPositionKeyboard:
                        Log.Info("Changing keyboard to Size & Position.");
                        Keyboard = new SizeAndPosition(() => Keyboard = currentKeyboard);
                        break;

                    case FunctionKeys.Speak:
                        var speechStarted = audioService.SpeakNewOrInterruptCurrentSpeech(
                            keyboardOutputService.Text,
                            () => { KeyStateService.KeyDownStates[KeyValues.SpeakKey].Value = KeyDownStates.Up; },
                            Settings.Default.SpeechVolume,
                            Settings.Default.SpeechRate,
                            Settings.Default.SpeechVoice);
                        KeyStateService.KeyDownStates[KeyValues.SpeakKey].Value = speechStarted ? KeyDownStates.Down : KeyDownStates.Up;
                        break;

                    case FunctionKeys.YesQuestionResult:
                        HandleYesNoQuestionResult(true);
                        break;
                }

                keyboardOutputService.ProcessFunctionKey(singleKeyValue.FunctionKey.Value);
            }
        }

        private void SetupFinalClickAction(Action<Point?> finalClickAction, bool finalClickInSeries = true, bool suppressMagnification = false)
        {
            nextPointSelectionAction = nextPoint =>
            {
                if (!suppressMagnification 
                    && keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value.IsDownOrLockedDown())
                {
                    ShowCursor = false; //Ensure cursor is not showing when MagnifyAtPoint is set because...
                    //1.This triggers a screen capture, which shouldn't have the cursor in it.
                    //2.Last popup open stays on top (I know the VM in MVVM shouldn't care about this, so pretend it's all reason 1).
                    MagnifiedPointSelectionAction = finalClickAction;
                    MagnifyAtPoint = nextPoint;
                    ShowCursor = true;
                }
                else
                {
                    finalClickAction(nextPoint);
                }

                if (finalClickInSeries)
                {
                    nextPointSelectionAction = null;
                }
            };

            SelectionMode = SelectionModes.Point;
            ShowCursor = true;
        }

        private void SimulateMouseDelta(int x, int y)
        {
            Action simulateMoveDelta = () =>
            {
                var delta = new Point(x, y);
                Log.InfoFormat("Performing mouse move by delta ({0},{1}).", delta.X, delta.Y);
                mouseOutputService.MoveBy(delta);
            };
            lastMouseActionStateManager.LastMouseAction = simulateMoveDelta;
            simulateMoveDelta();
        }

        private void ResetAndCleanupAfterMouseAction()
        {
            SelectionMode = SelectionModes.Key;
            nextPointSelectionAction = null;
            ShowCursor = false;
            MagnifyAtPoint = null;
            MagnifiedPointSelectionAction = null;
            if (keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value == KeyDownStates.Down)
            {
                keyStateService.KeyDownStates[KeyValues.MouseMagnifierKey].Value = KeyDownStates.Up; //Release magnifier if down but not locked down
            }
        }

        public void HandleServiceError(object sender, Exception exception)
        {
            Log.Error("Error event received from service. Raising ErrorNotificationRequest and playing ErrorSoundFile (from settings)", exception);

            inputService.RequestSuspend();
            audioService.PlaySound(Settings.Default.ErrorSoundFile, Settings.Default.ErrorSoundVolume);
            RaiseToastNotification(Resources.CRASH_TITLE, exception.Message, NotificationTypes.Error, () => inputService.RequestResume());
        }
    }
}
