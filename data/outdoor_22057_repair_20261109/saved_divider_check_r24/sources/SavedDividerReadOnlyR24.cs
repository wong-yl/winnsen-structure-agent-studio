using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

internal static partial class OutdoorShadeR145
{
    static void SavedDividerReadOnly(Dictionary<string,object> c,Dictionary<string,object> row)
    {
        Need(!Writable,"Saved divider reader must remain read-only");
        Root=(string)c["package_root"];Top=(string)c["top"];
        Need(Within(Root,Stage)&&Path.GetFileName(Root).StartsWith("generated_"),"Only an isolated generated candidate may be read");
        var before=Identity(Root);row["input_identity"]=before;
        try {
            var top=OpenTop(row);RmActivate(top,"saved_divider_independent_reopen");
            Need(top.ForceRebuild3(false),"Saved top rebuild failed");
            var owner=Doc("箱体竖隔板 焊接.SLDASM");RmActivate(owner,"saved_divider_direct_owner");
            Need(owner.IsOpenedReadOnly()&&owner.ForceRebuild3(false),"Saved divider owner must rebuild read-only");
            VerifyDividerRightRib(c,row);
            var parts=((Array)((AssemblyDoc)owner).GetComponents(true)).Cast<Component2>().ToArray();
            var right=parts.Single(p=>Path.GetFileName(p.GetPathName())=="OUT_RightSideRib.SLDPRT");
            var left=parts.Single(p=>Path.GetFileName(p.GetPathName())=="箱体侧板加强筋.SLDPRT");
            Need(right.ReferencedConfiguration=="Default"&&!right.IsSuppressed()&&!right.IsHidden(true),"Saved right rib configuration or visibility changed");
            row["saved_divider_direct_owner"]=new{
                owner=owner.GetPathName(),configuration=((Configuration)owner.GetActiveConfiguration()).Name,
                read_only=owner.IsOpenedReadOnly(),health=LayoutFeatureHealth(owner),
                right=new{name=right.Name2,path=right.GetPathName(),configuration=right.ReferencedConfiguration,
                    fixed_instance=right.IsFixed(),constrained=right.GetConstrainedStatus(),mate_count=((Array)right.GetMates()).Length,
                    transform=right.Transform2.ArrayData},
                left=new{name=left.Name2,path=left.GetPathName(),configuration=left.ReferencedConfiguration,
                    fixed_instance=left.IsFixed(),constrained=left.GetConstrainedStatus(),transform=left.Transform2.ArrayData},
                mates=LayoutMates(owner),mate_signatures=DividerMateSignatures(owner,right.Name2),dimensions=Dims(owner)
            };
            row["native_save_performed"]=false;row["full_rebuild_return"]=true;row["health_pass"]=true;
            Flush("saved_divider_direct_owner_verified");
        } finally {
            Sw.CloseAllDocuments(true);CheckIdentity(Root,before);
            row["final_identity"]=Identity(Root);row["files_unchanged"]=true;
        }
    }
}
