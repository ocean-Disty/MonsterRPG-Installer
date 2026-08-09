//////////////////////////////////////////////////////////////////////////////
// VoiceChat.cs  -  the one part of /voice that has to run on the player's PC
//////////////////////////////////////////////////////////////////////////////
//
// Everything else about voice chat lives on the server
// (Server_MonsterRPG/Server/Core/Core_VoiceChat.cs): it mints the token, builds
// the link and prints it. This file exists for a single call it cannot make.
//
// setClipboard() writes to the system clipboard of the machine that RUNS it.
// MonsterRPG runs on a DEDICATED server, so serverCmdVoice calling setClipboard
// would copy the link onto a machine nobody is sitting at - the script would
// succeed, the log would say nothing, and every player would be told to paste a
// link that was never on their clipboard. The copy has to happen here, one hop
// later, on the machine with the browser on it.
//
// WHY THE LINK NEEDS THIS AT ALL. It is ~90 characters and carries a 24-character
// single-use token, plus the room key in link mode. Nobody retypes that from a
// chat window. The server prints it twice - once as a <a:> click-through, once as
// plain text - and both have a failure mode the player cannot work around: the
// click opens their DEFAULT browser, which is often not the one they are in, and
// the plain-text copy assumes a chat log they can select text from, which the
// MonsterRPG chat panel is not. The clipboard is the route that survives both.
//
// THIS FILE PRINTS THE CONFIRMATION, and that is a correctness point rather than
// a style one. Client_MonsterRPG is optional. A player without it never receives
// this command, so if the server printed "it's on your clipboard" they would be
// told about a copy that did not happen and would paste something else. Printing
// from the same function that does the copying is what makes the two impossible
// to disagree.
//
// Gate note (ServerGate.cs): nothing here starts at file scope, reads
// ServerConnection or calls commandToServer, so rules 1-4 have nothing to hold
// on to. This is a leaf handler that runs once, when asked, and leaves nothing
// behind.
//////////////////////////////////////////////////////////////////////////////


// Sent by serverCmdVoice after it has printed the link. One argument: the URL.
function clientCmdMRPG_VoiceLink(%url)
{
	//An empty URL would clear whatever the player had copied and replace the link
	//they asked for with nothing - strictly worse than not acting at all.
	if(%url $= "")
		return;

	setClipboard(%url);

	//onServerMessage rather than a direct newChatHud_AddLine: ChatPanel.cs packages
	//onServerMessage, so this one call reaches the MonsterRPG chat panel when it is
	//up and the stock chat hud when it is not. Writing to either one directly would
	//put the line in the wrong window for half the players.
	//ONE LINE, AND SHORT. serverCmdVoice is down to two lines for the sake of keeping
	//the click at the top of the chat window; a three-line confirmation from this side
	//would hand back everything that trimming bought.
	onServerMessage("\c0Copied to your clipboard - \c3Ctrl+V\c0 if the click opened the wrong browser.");
}
