// +--------------------------------------+
// | Custom Torque Markup Language Parser |
// |                                      |
// | Author: Greek2me (11902)             |
// +--------------------------------------+
// | Allows for parsing of custom TML     |
// | formatting.                          |
// +--------------------------------------+
// | USAGE:                               |
// | -Create a function called            |
// |  "customTMLParser_IDENTIFIER", where |
// |  IDENTIFIER is a unique name for     |
// |  your parser. See the function,      |
// |  "customTMLParser_default", for an   |
// |  example.                            |
// |                                      |
// | -Parse your text like this:          |
// |  parseCustomTML(%str,%obj,%identi);  |
// | -%str is the string, %obj is the     |
// |  object, %identi is the identifier.  |
// | -%obj is not required but is HIGHLY  |
// |  recommended.                        |
// |                                      |
// | -You may use multiple identifiers by |
// |  separating them by tabs. The string |
// |  will be parsed according to all of  |
// |  them.                               |
// +--------------------------------------+
// | REQUIREMENTS:                        |
// | - libstr (Support_LibStr)            |
// +--------------------------------------+

if($customTMLParser::version >= 2)
	return;
$customTMLParser::version = 2;

$customTMLParser::leftBracket = "<";
$customTMLParser::rightBracket = ">";
$customTMLParser::div = ":";

$customTMLParser::listBulletIndent = 2;
$customTMLParser::listTextIndent = 3;

function customTMLParser_default(%obj,%value0,%value1,%value2,%value3,%value4,%value5,%value6,%value7,%value8,%value9,%value10,%value11,%value12,%value13,%value14,%value15)
{
	if(%obj.TML_listLevel $= "")
		%obj.TML_listLevel = 0;
	if(%obj.TML_listBulletIndent $= "")
		%obj.TML_listBulletIndent = $customTMLParser::listBulletIndent;
	if(%obj.TML_listTextIndent $= "")
		%obj.TML_listTextIndent = $customTMLParser::listBulletIndent;

	switch$(%value[0])
	{
		case "sPush":
			%obj.TML_oldFontTypes = setField(%obj.TML_oldFontTypes,getFieldCount(%obj.TML_oldFontTypes),%obj.TML_fontType);
			%obj.TML_oldFontSizes = setField(%obj.TML_oldFontSizes,getFieldCount(%obj.TML_oldFontSizes),%obj.TML_fontSize);

		case "sPop":
			%obj.TML_fontType = getField(%obj.TML_oldFontTypes,getFieldCount(%obj.TML_oldFontTypes) - 1);
			%obj.TML_fontSize = getField(%obj.TML_oldFontSizes,getFieldCount(%obj.TML_oldFontSizes) - 1);
			%obj.TML_oldFontTypes = removeField(%obj.TML_oldFontTypes,getFieldCount(%obj.TML_oldFontTypes) - 1);
			%obj.TML_oldFontSizes = removeField(%obj.TML_oldFontSizes,getFieldCount(%obj.TML_oldFontSizes) - 1);

		case "b":
			if(isObject(%obj) && striPos(%obj.TML_fontType,"bold") < 0)
				return true TAB "<sPush><font:" @ %obj.TML_fontType SPC "bold:" @ %obj.TML_fontSize @ ">";
			else
				return true TAB "<sPush><font:arial bold:15>";

		case "/b":
			return true TAB "<sPop>";

		case "i":
			if(isObject(%obj) && striPos(%obj.TML_fontType,"italic") < 0)
				return true TAB "<sPush><font:" @ %obj.TML_fontType SPC "italic:" @ %obj.TML_fontSize @ ">";
			else
				return true TAB "<sPush><font:arial italic:15>";

		case "/i":
			return true TAB "<sPop>";

		case "font":
			%obj.TML_fontType = %value[1];
			%obj.TML_fontSize = %value[2];

		case "size":
			return true TAB "<sPush><font:" @ %obj.TML_fontType @ ":" @ %value[1] @ ">";

		case "/size":
			return true TAB "<sPop>";

		case "colorHex":
			return true TAB "<sPush><color:" @ %value[1] @ ">";

		case "/color":
			return true TAB "<sPop>";

		case "/just":
			return true TAB "<just:left>";

		case "h1":
			return true TAB "<sPush><font:arial bold:24>";

		case "/h1":
			return true TAB "<sPop><br>";

		case "h2":
			return true TAB "<sPush><font:arial bold:20>";

		case "/h2":
			return true TAB "<sPop><br>";

		case "h3":
			return true TAB "<sPush><font:arial bold:17>";

		case "/h3":
			return true TAB "<sPop><br>";

		case "ol":
			%obj.TML_listLevel ++;
			%obj.TML_listMode[%obj.TML_listLevel] = "ol";
			%obj.TML_listIndex[%obj.TML_listLevel] = (%value[1] $= "" ? 0 : %value[1] - 1);
			return true TAB "";

		case "/ol":
			%obj.TML_listMode[%obj.TML_listLevel] = "";
			%obj.TML_listIndex[%obj.TML_listLevel] = "";
			%obj.TML_listLevel --;
			if(%obj.TML_listLevel == 0)
				return true TAB "<lmargin%:0>";
			else
				return true TAB "";

		case "ul":
			%obj.TML_listLevel ++;
			%obj.TML_listMode[%obj.TML_listLevel] = "ul";
			return true TAB "";

		case "/ul":
			%obj.TML_listMode[%obj.TML_listLevel] = "";
			%obj.TML_listLevel --;
			if(%obj.TML_listLevel == 0)
				return true TAB "<lmargin%:0>";
			else
				return true TAB "";

		case "li":
			%level = %obj.TML_listLevel;
			%indentBullet = %level * %obj.TML_listBulletIndent + (%level - 1) * %obj.TML_listTextIndent;
			%indentText = %indentBullet + %obj.TML_listTextIndent;

			if(%obj.TML_listMode[%obj.TML_listLevel] $= "ul")
			{
				%bullet = (%value[1] $= "" ? "<b>+</b>" : "<bitmap:" @ %value[1] @ ">");
				return true TAB "<br><lmargin%:" @ %indentBullet @ ">" @ %bullet @ "<lmargin%:" @ %indentText @ ">";
			}
			else if(%obj.TML_listMode[%obj.TML_listLevel] $= "ol")
			{
				%num = %obj.TML_listIndex[%obj.TML_listLevel] ++;
				return true TAB "<br><lmargin%:" @ %indentBullet @ "><b>" @ %num @ ".</b><lmargin%:" @ %indentText @ ">";
			}
			else
			{
				return true TAB "";
			}

		case "/li":
			if(%obj.TML_listLevel > 0)
			{
				%indentText = %obj.TML_listBulletIndent * (%obj.TML_listLevel - 1) + %obj.TML_listTextIndent;
				return true TAB "<lmargin%:" @ %indentText @ ">";
			}
			else
				return true TAB "";
	}

	return false;
}

//Parses custom TML formatting.
//@param	string string	The string to parse.
//@param	objectID obj	The GuiMLTextCtrl containing the string. (Optional)
//@param	string parserFunction	Used to parse the string. Place multiple in a tab-delimited list to parse in list order.
function parseCustomTML(%string,%obj,%parserFunction)
{
	if(%parserFunction $= "")
		%parserFunction = "default";
	%parserIndex = 0;

	if(isObject(%obj) && !%obj.TML_skipFont && %obj.getClassName() $= "GuiMLTextCtrl")
	{
		%text = %obj.getText();
		if(%text $= "" || %text $= %obj.text)
		{
			%obj.TML_fontType = %obj.profile.fontType;
			%obj.TML_fontSize = %obj.profile.fontSize;

			%obj.TML_oldFontTypes = "";
			%obj.TML_oldFontSizes = "";
		}
	}
	%obj.TML_skipFont = "";

	for(%i = 0; %i < strLen(%string); %i ++)
	{
		%char = getSubStr(%string,%i,1);

		if(%char $= $customTMLParser::leftBracket)
			%start = %i;

		if(%char $= $customTMLParser::rightBracket && %start !$= "")
		{
			%end = %i;

			for(%e = 0; %e < getFieldCount(%parserFunction); %e ++)
			{
				%full = getSubStr(%string,%start,%end-%start+1);
				%contents = getSubStr(%full,1,strLen(%full) - 2);

				%search = %contents;
				%pos = -1;
				%numValues = 0;
				while(strPos(%search,$customTMLParser::div) >= 0)
				{
					%search = getSubStr(%search,%pos+1,strLen(%search));

					%pos = strPos(%search,$customTMLParser::div);
					if(%pos >= 0)
					{
						%value[%numValues] = getSubStr(%search,0,%pos);
					}
					else
					{
						%value[%numValues] = %search;
					}
					%numValues ++;
				}
				if(%numValues <= 0 && %contents !$= "")
				{
					%value[0] = %contents;
					%numValues = 1;
				}

				%parser = getField(%parserFunction,%e);
				%replace = call("customTMLParser_" @ %parser,%obj,%value0,%value1,%value2,%value3,%value4,%value5,%value6,%value7,%value8,%value9,%value10,%value11,%value12,%value13,%value14,%value15);

				if(getField(%replace,0))
				{
					%obj.TML_skipFont = true;
					%parser = %parserFunction;
					if(striPos(%replace, %full) >= 0)
						%parser = removeField(%parser, %e);
					%replace = getFields(%replace,1);
					%replace = parseCustomTML(%replace, %obj, %parser); //getField(%parserFunction, %e + 1));
					if(%full !$= %replace)
					{
						%string = setSubStr(%string,%start,%end - %start + 1,%replace);

						%end = %start + strLen(%replace) - 1;
						%i = %end;
					}
					break;
				}
			}

			%start = "";
			%end = "";
			%full = "";
			%contents = "";
			%search = "";
			%replace = "";
			for(%e = 0; %e < %numValues; %e ++)
				%value[%e] = "";
			%numValues = "";
		}
	}

	return %string;
}