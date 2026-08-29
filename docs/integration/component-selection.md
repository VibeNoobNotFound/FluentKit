# Component selection

The table below is generated from the public assembly and the checked-in sample manifest.
Use it to find the smallest control that owns the behavior your feature needs.

| Area | Start here | Sample |
| --- | --- | --- |
| Buttons and actions | `FluentButton`, `FluentToggleButton`, `FluentIconButton`, `FluentSplitButton` | `/Primitives/Button` |
| Text and input | `FluentTextBox`, `FluentPasswordBox`, `FluentNumberBox`, `FluentComboBox` | `/Primitives/TextBox` |
| Selection | `FluentCheckBox`, `FluentRadioGroup`, `FluentToggleSwitch`, `FluentSlider` | `/Primitives/CheckBox` |
| Feedback | `FluentInfoBar`, `FluentInfoBadge`, `FluentProgressBar`, `FluentProgressRing` | `/Primitives/InfoBar` |
| Layout and display | `FluentCard`, `FluentExpander`, `FluentDivider`, `FluentTextBlock` | `/Primitives/Card` |
| Overlays | `FluentTooltip`, `FluentFlyout`, `FluentContextMenu`, `FluentContentDialog`, `FluentTeachingTip` | `/Composite/Flyout` |
| Navigation | `FluentNavigationView`, `FluentMenuBar`, `FluentPivot` | `/Composite/NavigationView` |
| Effects | `FluentMicaPanel`, `FluentAcrylicBrush`, `FluentRevealBackground` | `/Effects/Mica` |

For the exact parameter and event surface, use the package-local `api.md` generated from the
same assembly as this guide.
