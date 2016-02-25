To compile an XML project:
MassEffect3.Coalesce.exe -b <Source> <Destination>

Source should be an ".xml" file, like Coalesced.xml (or what ever you decide to name it as).
Destination should be a ".bin" file

To Decompile a Coalesced.bin (Main or DLC):
MassEffect3.Coalesce.exe 


You can also simple drag and drop the ".xml" or ".bin" and it will compile/decompile to the directory the file exists with the same name as the source file.

There are a couple of useful attributes available, however one more or less is required in the BioCredits to work right.

By default, it treats all values/properties as unique and if it exists it with either skip it or overright it, so you need to add allowDuplicates="true"
to the Property.  In this case it would look something like this: <Property name="credits" allowDuplicates="true">.

The attribute "type" is the same as it was in the JSON, only the compiler handles each time correctly now.

Values of the types: 0, 1, 2 and 3 all get added, type 4 removes the value from the property, must be exact match, but not case sensitive nor space sensitive as it get's trimmed before it's used.

Valid value types:
type="-1"
type="0"
type="1"
type="2"
type="3"
type="4"

Note: Type -1 is not added to the Coalesced file, it currently acts as a way to clear all values from a property without the null value getting added.
Reason for this special case is, null is used in several locations within the DLC's as and is needed.

Example:

	<Section name="sfxgame.sfxgame">
		<Property name="newgameplusplayervariables">
			<Value type="3">SFXGameContent.SFXWeapon_AssaultRifle_Mattock</Value>
			<Value type="3">SFXGameContent.SFXWeapon_AssaultRifle_Revenant</Value>
			<Value type="3">SFXGameContent.SFXWeapon_AssaultRifle_Saber</Value>
			<Value type="3">SFXGameContent.SFXWeapon_AssaultRifle_Vindicator</Value>
			<Value type="3">SFXGameContent.SFXWeapon_AssaultRifle_Argus</Value>
			<Value type="3">SFXGameContent.SFXWeapon_AssaultRifle_Reckoning</Value>
			<Value type="3">SFXGameContent.SFXWeapon_AssaultRifle_Valkyrie</Value>
			
			<Value type="-1">null</Value>
			<Value type="2">SFXGameContent.SFXWeapon_AssaultRifle_Avenger</Value>
			<Value type="2">SFXGameContent.SFXWeapon_AssaultRifle_Cobra</Value>
			<Value type="2">SFXGameContent.SFXWeapon_AssaultRifle_Collector</Value>
			<Value type="2">SFXGameContent.SFXWeapon_AssaultRifle_Falcon</Value>
			<Value type="2">SFXGameContent.SFXWeapon_AssaultRifle_Geth</Value>
		</Property>
	</Section>
	
	In this way, the values before <Value type="-1">null</Value> will be removed and then only the values after it will be added.
	Added this so I can modify array's quickly without having to use type="4" for each value and hope they match.
	
	type="1" will also do this, but it DOES pass the value onto the Coalesced.
	

Another attribute is: ignore="false|true"
When set to true, it completely skips reading the value/property.


Attributes for the "Section" element are:
	name (Required)


Attributes for the "Property" element are:
	name (Required)
	ignore (Must be true or false) (Default is false)
	allowDuplicates (Must be true of false) (Default is false)
	type (Pptional) (Defaults to 2)


Attributes for the "Value" element are:
	ignore (must be true or false) (Default is false)
	type (Optional) (Default is 2)

The next thing is the "Includes" element:
	<Includes>
		<Include source="Relative|Absolute path" />
	</Includes>
	
	Keep in mind, any "Include" elements are parsed before the "Sections" element.
	

CoalesceAsset is for the files containing "Sections" and "Includes", can see this inside the xml files from decompiled ".bin" files.

It has the following attributes
	id (Optional, not yet used)
	name (Optional, not yet used)
	source (Required) (Shouldn't have to change it)


Last but not least, the most important file, "CoalesceFile"

CoalesceFile is the start point for the compiler and goes through each of the Asset source paths and parses everything mentioned above this.

It also has an element "Settings", demonstrated below:

	<CoalesceFile>
		<Assets>
			<Asset source="Relative|Absolute" />
		</Assets>
		<Settings>
			<Setting name="ForceValueType" value="false|true" />
			<Setting name="ForcedValueType" value="2" />
		</Settings>
	</CoalesceFile>
	
	
The attributes available to "CoalesceFile":
	id (Optional, not yet used)
	name (Optional, not yet used)
	source (Optional, not yet used)

The attributes available to "Asset":
	source (Required) (Relative or absolute path)

The attributes available to "Setting":
	name (Required)
	value (Required)
	
The available settings are:
	ForceValueType, must be true or false
	ForcedValueType, must be a valid type (0, 1, 2, 3 or 4)
	

That should cover everything, can always look in the XML files to see how they function.

The SampleMod does not change anything, it just demonstrates how you can split the Coalesced.bin into multiple xml that have includes.