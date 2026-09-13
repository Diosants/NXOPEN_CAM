using System;
using System.Collections.Generic;
using NXOpen;

public class LISTAR_FEATURE_GROUPS
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;

        if (workPart == null)
            return;

        NXOpen.ListingWindow lw = theSession.ListingWindow;
        lw.Open();

        NXOpen.CAM.CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();

        List<string> names = new List<string>();

        foreach (NXOpen.CAM.CAMObject obj in objects)
        {
            NXOpen.CAM.FeatureGeometryGroup fg = obj as NXOpen.CAM.FeatureGeometryGroup;
            if (fg == null)
                continue;

            // Pega só os grupos que começam com FG_STEP, FG_HOLE ou FG_WEDM_ROUND.
            if (fg.Name.StartsWith("FG_STEP") ||
                fg.Name.StartsWith("FG_HOLE") ||
                fg.Name.StartsWith("FG_WEDM_ROUND"))
                names.Add(fg.Name);
        }

        lw.WriteLine("Feature groups encontrados (" + names.Count + "):");
        foreach (string n in names)
        {
            lw.WriteLine("  " + n);
        }
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}
