using System;
using System.Collections.Generic;
using System.Linq;

public class GameExclusion {
    public string Id="", Name="", Folder="";
    public override string ToString(){return Name+(String.IsNullOrEmpty(Id)?"":"  (Steam "+Id+")");}
}
public static class Exclusions {
    public static bool Matches(GameExclusion rule,string id,string folder,string name){
        // IDs distinguish similarly named games. Folder/name cover discovery fallbacks.
        if(!String.IsNullOrEmpty(rule.Id)&&!String.IsNullOrEmpty(id))return rule.Id==id;
        if(!String.IsNullOrEmpty(rule.Folder)&&!String.IsNullOrEmpty(folder))
            return String.Equals(rule.Folder.TrimEnd('\\','/'),folder.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);
        return !String.IsNullOrEmpty(rule.Name)&&String.Equals(rule.Name,name,StringComparison.OrdinalIgnoreCase);
    }
    public static bool Contains(IEnumerable<GameExclusion> rules,string id,string folder,string name){return rules!=null&&rules.Any(r=>Matches(r,id,folder,name));}
}
