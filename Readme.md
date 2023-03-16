# ThreeHousesSlave

This is a console tool that can indentify some types of Fire Emblem Three Houses text files automatically and extract/insert the text, it can't repack or extract the DATA/INFO.BIN

## How to use
- Install the **.NET 6.0**
- Use [get_index_data.py](https://github.com/3096/koeipy) to extract the DATA.BIN
- Drag&Drop the out directory to the ThreeHousesSlave
	-  This will made the tool look for the text files and rename with the prefix **_str** or **_scene**  
- Drag&Drop a directory with **_str** or **_scene** bin files
	- This will made the tool extract the text of the given files
- Drag&Drop the directory with the extracted txt files
	- This will made the tool insert the extracted txt files
- Use [pack_info.py](https://github.com/3096/koeipy) to create your translation mod
