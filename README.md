<img width="1732" height="300" alt="UI" src="https://github.com/user-attachments/assets/2d7e00f3-b13f-4933-9cad-b02610c539d6" />

# Crescent Mod Manager
A lightweight mod manager capable of working with (almost) any game on Nexus Mods. Currently supports:
- [Stardew Valley](https://www.nexusmods.com/games/stardewvalley)
- [Cyberpunk 2077](https://www.nexusmods.com/games/cyberpunk2077)

> *Note, install process is kinda iffy at the moment. I'll set up an installer for everything eventually maybe (or not)*
## Install
- Crescent uses Windows Symlinks to create "pointers" to mod files and assets. Please enable the setting below before continuing:
  - Open the Windows Settings app.
  - Navigate to `System\Advanced` and scroll down to `For Developers`
  - Turn **Developer Mode** on and accept the prompt
- Download the latest release [here](https://github.com/Silk15/Crescent-Mod-Manager/releases/tag/MainFiles) and extract it somewhere on your PC.
  - Navigate to `Crescent Mod Manager\Plugins` and install your required games from [here](https://github.com/Silk15/Crescent-Mod-Manager/tree/plugins)
  - Navigate to `Crescent Mod Manager\Managed` and scroll until you see the `.exe`
 
And that's it. When you start Crescent you'll be able to set staging and executable paths like any other mod manager, as well as import profiles.


<img width="1495" height="931" alt="Screenshot 2026-11-01 220946" src="https://github.com/user-attachments/assets/cf14e344-d8eb-4d56-9d14-aa6e898ffd09" />


> *Parts of this project were written with AI, such as the **base** of the UI, Nexus API requsts and a good chunk of the file deployment. However most if not all code has been majorly refactored to actually work. Both in the way I want and literally. However I refuse to ever entirely vibe code something or paste code in, especially not without extensively checking it.*
