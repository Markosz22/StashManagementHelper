using System;
using System.Reflection;
using EFT.UI;
using UnityEngine;

namespace StashManagementHelper.Helpers
{
    /// <summary>
    /// Extension methods for the <see cref="InteractionButtonsContainer"/> class to access private members via reflection.
    /// </summary>
    internal static class InteractionButtonsContainerExtensions
    {
        private static readonly FieldInfo ButtonsContainerField = typeof(InteractionButtonsContainer)
            .GetField("_buttonsContainer", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Gets the private _buttonsContainer <see cref="RectTransform"/> from an <see cref="InteractionButtonsContainer"/> instance.
        /// </summary>
        public static RectTransform GetButtonsContainer(this InteractionButtonsContainer instance)
            => ButtonsContainerField.GetValue(instance) as RectTransform;

        private static readonly FieldInfo ButtonTemplateField = typeof(InteractionButtonsContainer)
            .GetField("_buttonTemplate", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Gets the private _buttonTemplate <see cref="SimpleContextMenuButton"/> from an <see cref="InteractionButtonsContainer"/> instance.
        /// </summary>
        public static SimpleContextMenuButton GetButtonTemplate(this InteractionButtonsContainer instance)
            => ButtonTemplateField.GetValue(instance) as SimpleContextMenuButton;

        // Reflection cache to close existing submenu entries without hiding the whole container.
        // NOTE: "method_4" is an obfuscated name discovered via reverse engineering.
        // It is extremely likely to change between game versions, which would break this functionality.
        private static readonly MethodInfo CloseSubMenuMethod = typeof(InteractionButtonsContainer)
            .GetMethod("method_4", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        static InteractionButtonsContainerExtensions()
        {
            if (CloseSubMenuMethod == null)
                Debug.LogError("[InteractionButtonsContainerExtensions] method_4 not found; CloseSubMenu will be skipped.");
        }

        /// <summary>
        /// Clears out any previously added submenu buttons by invoking the private "method_4".
        /// </summary>
        public static void CloseSubMenu(this InteractionButtonsContainer instance)
        {
            if (instance == null)
            {
                Debug.LogError("[InteractionButtonsContainerExtensions] instance is null in CloseSubMenu");
                return;
            }
            if (CloseSubMenuMethod == null)
            {
                Debug.LogError("[InteractionButtonsContainerExtensions] CloseSubMenuMethod is null");
                return;
            }
            try
            {
                CloseSubMenuMethod.Invoke(instance, null);
            }
            catch (Exception ex)
            {
                Debug.LogError("[InteractionButtonsContainerExtensions] Exception invoking CloseSubMenu: " + ex);
            }
        }
    }
}