# StashManagementHelper

A Single Player Tarkov (SPT) plugin that enhances stash management with configurable sorting capabilities.

## Features

### Customizable Stash Sorting
- Create your own sorting strategy or enhance the default EFT sorting
- Automatically fold items to optimize space
- Merge identical item stacks
- Smart item rotation for optimal fit
- Configurable empty rows at the top for item dumping
- Bottom-up sorting option  

![{1D31F86E-3274-4D1D-B9E6-FB12E472A80E}](https://github.com/user-attachments/assets/f2339a85-65ef-4e22-9bdc-6d01b43a2270)

### Sorting Strategies
Configure sorting based on multiple criteria:
- Container size
- Item size
- Item type (with customizable type ordering - Not working too great, work in progress)
- Item weight
- Item value

![image](https://github.com/Markosz22/StashManagementHelper/assets/41615461/521ecce7-ceda-45cc-8ab2-7539cb11b550)

## Usage

1. Configure settings according to your preferences (default settings optimized for maximum space efficiency)
2. Press the sort button
3. Global options (1) apply to both default and custom sorting methods
4. Right-click the sort button to open a quick-action menu:
   - **Default Sort**: based on your configured sorting options.
   - **Sort by Value**: sorts items by descending value.
   - **Sort by Weight**: sorts items by descending weight.

### Custom Configuration
After first use, a `customSortConfig.json` file will be generated next to the plugin DLL. This file allows you to:
- Modify `sortOrder` to prioritize different sorting criteria
- Customize `itemTypeOrder` to define the sequence of item types when using type-based sorting
