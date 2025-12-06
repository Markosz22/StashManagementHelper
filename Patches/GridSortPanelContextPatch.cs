using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;
using StashManagementHelper.Configuration;
using StashManagementHelper.SortingStrategy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StashManagementHelper.Patches
{
    public class GridSortPanelContextPatch : ModulePatch
    {
        private const string SortButtonName = "Sort";
        private const string IconNameSubstring = "icon";
        private const string ArialFontPath = "Arial.ttf";
        private const string MenuCanvasName = "SMH_MenuCanvas";
        private const string MenuBlockerName = "CustomMenuBlocker";
        private const string CustomSortMenuName = "CustomSortMenu";
        private const string IconGameObjectName = "Icon";
        private const string LabelGameObjectName = "Label";

        // Context menu instance
        private static GameObject _customMenu;
        private static Button _sortBtn;
        private static GameObject _buttonTemplate;
        // Full-screen blocker to close menu on outside click
        private static GameObject _menuBlocker;
        // Overlay canvas for the menu
        private static GameObject _menuCanvasObj;

        /// <summary>
        /// The Harmony patch target method.
        /// </summary>
        /// <returns>The method to be patched.</returns>
        protected override MethodBase GetTargetMethod()
        {
            // Patch the Show method of the stash sort panel (actual type contains DragAndDrop namespace)
            var type = AccessTools.TypeByName("EFT.UI.DragAndDrop.GridSortPanel");
            if (type == null)
            {
                Debug.LogError("[GridSortPanelContextPatch] Type EFT.UI.DragAndDrop.GridSortPanel not found. Patching aborted.");
                return null;
            }
            var method = AccessTools.Method(type, "Show");
            if (method == null)
            {
                Debug.LogError($"[GridSortPanelContextPatch] Show method not found on GridSortPanel ({type.FullName}). Patching aborted.");
                return null;
            }
            return method;
        }

        /// <summary>
        /// Harmony Postfix patch for the Show method of the GridSortPanel.
        /// Adds a right-click context menu to the sort button.
        /// </summary>
        /// <param name="__instance">The instance of GridSortPanel.</param>
        [PatchPostfix]
        private static void Postfix(object __instance)
        {
            if (!(__instance is Component panel))
            {
                Debug.LogWarning("[GridSortPanelContextPatch] Instance is not a Component, cannot proceed with Postfix.");
                return;
            }

            var currentSortBtn = panel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(b => b.name.IndexOf(SortButtonName, StringComparison.OrdinalIgnoreCase) >= 0);

            if (currentSortBtn == null)
            {
                Debug.LogWarning($"[GridSortPanelContextPatch] Could not find sort button with '{SortButtonName}' in its name in panel children. Postfix aborted.");
                return;
            }

            if (currentSortBtn.GetComponent<SMHSortButtonMarker>() != null)
            {
                return;
            }

            var eventTrigger = currentSortBtn.gameObject.GetComponent<EventTrigger>() ?? currentSortBtn.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(data => OnPointerClick((PointerEventData)data, currentSortBtn));
            eventTrigger.triggers.Add(entry);

            currentSortBtn.gameObject.AddComponent<SMHSortButtonMarker>();
        }

        /// <summary>
        /// Handles pointer click events on the sort button to show the custom context menu on right-click.
        /// </summary>
        /// <param name="eventData">The pointer event data.</param>
        /// <param name="clickedButton">The button that was clicked.</param>
        private static void OnPointerClick(PointerEventData eventData, Button clickedButton)
        {
            if (eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }

            if (clickedButton == null)
            {
                Debug.LogWarning("[GridSortPanelContextPatch] Sort button reference is null, cannot show custom menu.");
                return;
            }

            _sortBtn = clickedButton;

            DestroyCustomMenu();
            ShowCustomMenu(eventData);
        }

        /// <summary>
        /// Destroys the custom menu and its associated GameObjects.
        /// </summary>
        private static void DestroyCustomMenu()
        {
            if (_menuCanvasObj != null)
            {
                GameObject.Destroy(_menuCanvasObj);
            }
            _menuCanvasObj = null;
            _customMenu = null;
            _menuBlocker = null;
        }

        /// <summary>
        /// Creates and displays the custom context menu at the cursor's position.
        /// </summary>
        /// <param name="eventData">The pointer event data from the click event.</param>
        private static void ShowCustomMenu(PointerEventData eventData)
        {
            // Cache the original sort button as the template
            if (_buttonTemplate == null && _sortBtn != null)
            {
                _buttonTemplate = _sortBtn.gameObject;
            }

            // Create overlay canvas for the menu
            _menuCanvasObj = new GameObject(MenuCanvasName);
            var menuCanvas = _menuCanvasObj.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            menuCanvas.sortingOrder = 5000; // Set high sorting order for overlay
            _menuCanvasObj.AddComponent<GraphicRaycaster>();

            var canvas = menuCanvas;
            var canvasRt = _menuCanvasObj.GetComponent<RectTransform>();

            // Create full-screen blocker to catch outside clicks
            _menuBlocker = new GameObject(MenuBlockerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _menuBlocker.transform.SetParent(canvas.transform, false);
            _menuBlocker.layer = 5; // UI Layer
            var blockerRt = _menuBlocker.GetComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero;
            blockerRt.offsetMax = Vector2.zero;
            var blockerImg = _menuBlocker.GetComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0f);
            blockerImg.raycastTarget = true;
            var blockerBtn = _menuBlocker.GetComponent<Button>();
            blockerBtn.onClick.AddListener(() => DestroyCustomMenu());

            _customMenu = new GameObject(CustomSortMenuName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _customMenu.transform.SetParent(canvas.transform, false);
            _customMenu.transform.SetAsLastSibling(); // Render menu above blocker
            _customMenu.layer = 5; // UI Layer

            var menuRt = _customMenu.GetComponent<RectTransform>();
            menuRt.pivot = new Vector2(0, 1); // Top-left pivot

            // Position the menu at the cursor's click position
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, eventData.position, null, out var localPos))
            {
                Debug.LogError("[GridSortPanelContextPatch] Failed to convert screen point to local point for menu positioning.");
                DestroyCustomMenu();
                return;
            }

            menuRt.anchoredPosition = localPos;

            var bg = _customMenu.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.8f);
            bg.raycastTarget = true;

            var layout = _customMenu.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 2;
            layout.padding = new RectOffset(5, 5, 5, 5);

            var fitter = _customMenu.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            //CreateMenuButton("Sort by Market Value", () => { SwapFlags("FleaValue"); DestroyCustomMenu(); }); //TODO
            CreateMenuButton("Sort by Trader Value", () => { SwapFlags("Value"); DestroyCustomMenu(); });
            CreateMenuButton("Sort by Item Weight", () => { SwapFlags("Weight"); DestroyCustomMenu(); });

            _customMenu.SetActive(true);
        }

        /// <summary>
        /// Creates a single button for the custom menu.
        /// </summary>
        /// <param name="caption">The text to display on the button.</param>
        /// <param name="action">The action to perform when the button is clicked.</param>
        private static void CreateMenuButton(string caption, Action action)
        {
            // Clone the original sort button as template
            var btnObj = UnityEngine.Object.Instantiate(_buttonTemplate, _customMenu.transform, false);
            btnObj.name = caption + "Button";

            // Remove any old triggers or markers
            var oldTrigger = btnObj.GetComponent<EventTrigger>();
            if (oldTrigger != null) GameObject.Destroy(oldTrigger);
            var oldMarker = btnObj.GetComponent<SMHSortButtonMarker>();
            if (oldMarker != null) GameObject.Destroy(oldMarker);

            // Reset click handlers and bind new action
            var btn = btnObj.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => action());

            // Update background visuals
            var btnImage = btnObj.GetComponent<Image>();
            btnImage.raycastTarget = true;

            // Ensure the button has a LayoutElement to control its size
            var layoutElement = btnObj.GetComponent<LayoutElement>() ?? btnObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 200;
            layoutElement.preferredHeight = 30;

            // Clear existing text and image children from the cloned template to ensure a clean slate for our custom layout
            // Iterate through children to find and destroy Text and non-background Image components.
            // We iterate backwards to safely destroy GameObjects without affecting the loop.
            for (int i = btnObj.transform.childCount - 1; i >= 0; i--)
            {
                var child = btnObj.transform.GetChild(i);
                if (child.GetComponent<Text>() != null)
                {
                    GameObject.Destroy(child.gameObject);
                }
                // Destroy any image child that is not the button's own background image
                var childImage = child.GetComponent<Image>();
                if (childImage != null && childImage != btnImage)
                {
                    GameObject.Destroy(child.gameObject);
                }
            }

            // Add horizontal layout group to arrange icon and text
            // Remove any existing LayoutGroup to avoid duplicates
            foreach (var group in btnObj.GetComponents<LayoutGroup>())
            {
                UnityEngine.Object.DestroyImmediate(group);
            }
            var horizontalLayout = btnObj.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = true;
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.spacing = 4f;
            horizontalLayout.padding = new RectOffset(4, 4, 4, 4);

            // Use helper methods for icon and text creation
            AddIconToButton(btnObj);
            AddTextToButton(btnObj, caption);
        }

        /// <summary>
        /// Finds and adds an icon to a menu button, based on the icon from the original sort button.
        /// </summary>
        /// <param name="btnObj">The button GameObject to add the icon to.</param>
        private static void AddIconToButton(GameObject btnObj)
        {
            var originalIcon = _sortBtn.GetComponentInChildren<Image>(true);

            Image iconSource = null;
            if (originalIcon != null && originalIcon != _sortBtn.GetComponent<Image>())
            {
                iconSource = originalIcon;
            }
            else
            {
                var allImages = _sortBtn.GetComponentsInChildren<Image>(true);

                var iconCandidate = allImages.FirstOrDefault(img =>
                    img != _sortBtn.GetComponent<Image>() &&
                    img.sprite != null &&
                    img.name.IndexOf(IconNameSubstring, StringComparison.OrdinalIgnoreCase) >= 0);

                if (iconCandidate == null)
                {
                    iconCandidate = allImages.FirstOrDefault(img =>
                        img != _sortBtn.GetComponent<Image>() &&
                        img.sprite != null);
                }

                iconSource = iconCandidate;
            }

            if (iconSource != null)
            {
                CreateIconObject(iconSource.sprite, iconSource.color, btnObj.transform);
            }
            else
            {
                Debug.LogWarning($"[GridSortPanelContextPatch] No suitable icon found for button '{btnObj.name}'");
            }
        }

        /// <summary>
        /// Creates the icon's GameObject and components.
        /// </summary>
        /// <param name="sprite">The icon sprite.</param>
        /// <param name="color">The icon color.</param>
        /// <param name="parent">The parent transform for the icon.</param>
        private static void CreateIconObject(Sprite sprite, Color color, Transform parent)
        {
            var iconObj = new GameObject(IconGameObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(parent, false);

            var iconImage = iconObj.GetComponent<Image>();
            iconImage.sprite = sprite;
            iconImage.color = color;
            iconImage.raycastTarget = false;

            var iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = sprite.rect.width;
            iconLayout.preferredHeight = 0; // Let aspect ratio fitter control height
            iconLayout.flexibleWidth = 0f;
            iconLayout.flexibleHeight = 0f;

            // Add aspect ratio fitter to preserve icon proportions
            var aspectFitter = iconObj.AddComponent<AspectRatioFitter>();
            aspectFitter.aspectRatio = sprite.rect.width / sprite.rect.height;
            aspectFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        }

        /// <summary>
        /// Creates the text label for a menu button.
        /// </summary>
        /// <param name="btnObj">The button GameObject to add the text to.</param>
        /// <param name="caption">The text to display.</param>
        private static void AddTextToButton(GameObject btnObj, string caption)
        {
            var txtObj = new GameObject(LabelGameObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            txtObj.transform.SetParent(btnObj.transform, false);

            var txtComp = txtObj.GetComponent<Text>();
            txtComp.text = caption;
            txtComp.alignment = TextAnchor.MiddleCenter;
            txtComp.color = Color.white;
            txtComp.font = Resources.GetBuiltinResource<Font>(ArialFontPath);
            txtComp.fontSize = 14;
            txtComp.raycastTarget = false;

            var txtLayout = txtObj.AddComponent<LayoutElement>();
            txtLayout.flexibleWidth = 1f;
            txtLayout.flexibleHeight = 1f;

            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Temporarily changes the sorting options and triggers the sort.
        /// </summary>
        /// <param name="mode">The sort mode to apply ("Value" or "Weight").</param>
        private static void SwapFlags(string mode)
        {
            Settings.BackupSortOptions();

            // Disable all, enable only selected mode
            Settings.ContainerSize.Value = SortOptions.None;
            Settings.CellSize.Value = SortOptions.None;
            Settings.ItemType.Value = SortOptions.None;
            Settings.Weight.Value = (mode == "Weight") ? (SortOptions.Enabled | SortOptions.Descending) : SortOptions.None;
            Settings.TraderValue.Value = (mode == "Value") ? (SortOptions.Enabled | SortOptions.Descending) : SortOptions.None;
            Settings.MarketValue.Value = (mode == "FleaValue") ? (SortOptions.Enabled | SortOptions.Descending) : SortOptions.None;

            // Invoke the existing sort button
            _sortBtn.onClick.Invoke();
        }

        private class SMHSortButtonMarker : MonoBehaviour { }
    }
}