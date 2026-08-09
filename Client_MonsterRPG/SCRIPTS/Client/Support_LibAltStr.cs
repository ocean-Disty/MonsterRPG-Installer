////STRING support


if(isObject(String))
    String.delete();

new scriptObject(String) {
    
};


function String::replace(%this,%str,%repl,%with) {
    return strReplace(%str,%repl,%with);
}

function String::contains(%this,%str,%contains) {
    if(stripos(%str,%contains) >= 0) {
        return true;
    } else {
        return false;
    }
}

function String::indexOf(%this,%str,%needle) {
    return stripos(%str,%needle);
}

function String::length(%this,%str) {
    return strLen(%str);
}

function String::split(%this,%str,%split) {
    %group = new scriptGroup();
    if(%split !$= " ") {
        %_msg = %this.replace(%str," ","_!!_");
        %_msg = %this.replace(%_msg,%split," ");
    } else {
        %_msg = %this.replace(%str,%split," ");
    }
    echo("message -> " @ %_msg);
    for(%i=0;%i<getWordCount(%_msg);%i++){
        %word = getWord(%_msg,%i);
        %o = new scriptObject() {
            value = %this.replace(%this.replace(%word," ",%split),"_!!_"," ");
        };
        %group.add(%o);
    }
    if(%group.getCount() > 0)
        return %group;
    else
        return false;
}

function String::join(%this,%str_array,%join) {
    if(isObject(%str_array)) {
        %_join = "";
        for(%i=0;%i<%str_array.getCount();%i++) {
            %word = %str_array.getObject(%i);
            if(%word.ignore == true)
                continue;
            if(%_join $= "")
                %_join = %word.value;
            else
                %_join = %_join @ %join @ %word.value;
        }
        return %_join;
    }
    return false;
}
///