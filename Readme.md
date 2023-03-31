# ThreeHousesSlave

This is a console tool that can indentify some types of Fire Emblem Three Houses text files automatically and extract/insert the text

## How to use
- Install the **.NET 6.0**
- Use [get_index_data.py](https://github.com/3096/koeipy) to extract the DATA.BIN
- Drag&Drop the out directory to the ThreeHousesSlave
	-  This will made the tool look for the text files and rename with the sufix **_str** or **_scene**  
- Drag&Drop a directory with **_str** or **_scene** bin files
	- This will made the tool extract the text of the given files
- Drag&Drop the directory with the extracted txt files
	- This will made the tool insert the extracted txt files
- Drag&Drop the INFO0.BIN to update the patch file index
