
using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.Assemblies;

public class WORKPLANE_TOP_BLOCK
{
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        Part displayPart = theSession.Parts.Display;


        List<Body> solidBodies = GetAllSolidBodies(workPart);

        if (solidBodies.Count == 0)
        {
            theSession.ListingWindow.Open();
            theSession.ListingWindow.WriteLine(
                "Nenhum corpo solido encontrado no modelo. Abortando.");
            return;
        }

        Body[] bodies1 = solidBodies.ToArray();

        NXOpen.CAM.FeatureGeometry featureGeometry1 =
            (NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE");

        NXOpen.CAM.NCGroup nCGroup1 =
            workPart.CAMSetup.CAMGroupCollection.CreateGeometryWithUserName(
                featureGeometry1,
                "mill_planar",
                "WORKPIECE",
                NXOpen.CAM.NCGroupCollection.UseDefaultName.False,
                "WP",
                "Workpiece 1");

        NXOpen.CAM.FeatureGeometry featureGeometry2 =
            (NXOpen.CAM.FeatureGeometry)nCGroup1;

        NXOpen.CAM.MillGeomBuilder millGeomBuilder1 =
            workPart.CAMSetup.CAMGroupCollection.CreateMillGeomBuilder(featureGeometry2);

        millGeomBuilder1.PartGeometry.InitializeData(false);

        NXOpen.CAM.GeometrySetList geometrySetList1 =
            millGeomBuilder1.PartGeometry.GeometryList;

        NXOpen.TaggedObject taggedObject1 =
            geometrySetList1.FindItem(0);

        NXOpen.CAM.GeometrySet geometrySet1 =
            (NXOpen.CAM.GeometrySet)taggedObject1;

        SelectionIntentRuleOptions selectionIntentRuleOptions1 =
            workPart.ScRuleFactory.CreateRuleOptions();

        selectionIntentRuleOptions1.SetSelectedFromInactive(false);

        NXOpen.BodyDumbRule bodyDumbRule1 =
            workPart.ScRuleFactory.CreateRuleBodyDumb(bodies1, true, selectionIntentRuleOptions1);

        selectionIntentRuleOptions1.Dispose();

        NXOpen.ScCollector scCollector1 =
            geometrySet1.ScCollector;

        NXOpen.SelectionIntentRule[] rules1 =
            new NXOpen.SelectionIntentRule[1];
        rules1[0] = bodyDumbRule1;
        scCollector1.ReplaceRules(rules1, false);

        NXOpen.NXObject nXObject1 =
            millGeomBuilder1.Commit();

        millGeomBuilder1.Destroy();

        // ------------------------------------------------------------
        // 3) BLANK GEOMETRY - Auto Block a partir dos mesmos solidos
        // ------------------------------------------------------------
        NXOpen.CAM.FeatureGeometry featureGeometry3 =
            (NXOpen.CAM.FeatureGeometry)nXObject1;

        NXOpen.CAM.MillGeomBuilder millGeomBuilder2 =
            workPart.CAMSetup.CAMGroupCollection.CreateMillGeomBuilder(featureGeometry3);

        millGeomBuilder2.BlankGeometry.InitializeData(false);

        millGeomBuilder2.BlankGeometry.BlankDefinitionType =
            NXOpen.CAM.GeometryGroup.BlankDefinitionTypes.AutoBlock;

        NXOpen.Features.ToolingBox nullToolingBox = null;
        NXOpen.Features.ToolingBoxBuilder toolingBoxBuilder1 =
            workPart.Features.ToolingFeatureCollection.CreateToolingBoxBuilder(nullToolingBox);

        NXOpen.NXObject[] selections1 = bodies1;
        NXOpen.NXObject[] deselections1 = new NXOpen.NXObject[0];
        toolingBoxBuilder1.SetSelectedOccurrences(selections1, deselections1);

        NXOpen.NXObject nXObject2 =
            millGeomBuilder2.Commit();

        millGeomBuilder2.Destroy();

 
        NXOpen.CAM.FeatureGeometry featureGeometry4 =
            (NXOpen.CAM.FeatureGeometry)nXObject2;

        NXOpen.CAM.MillGeomBuilder millGeomBuilder3 =
            workPart.CAMSetup.CAMGroupCollection.CreateMillGeomBuilder(featureGeometry4);

        NXOpen.NXObject nXObject3 =
            millGeomBuilder3.Commit();

        millGeomBuilder3.Destroy();

        theSession.CAMSession.Utils.SetInspectionIntent(false);

        // ------------------------------------------------------------
        // 5) MCS_LOCAL - orientacao (mantido, ja e generico por natureza)
        // ------------------------------------------------------------
        NXOpen.CAM.OrientGeometry orientGeometry1 =
            (NXOpen.CAM.OrientGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("MCS_LOCAL");

        theSession.CAMSession.PathDisplay.ShowToolPath(orientGeometry1);

        NXOpen.CAM.MillOrientGeomBuilder millOrientGeomBuilder1 =
            workPart.CAMSetup.CAMGroupCollection.CreateMillOrientGeomBuilder(orientGeometry1);

        millOrientGeomBuilder1.McsLocationMode =
            NXOpen.CAM.OrientGeomBuilder.McsLocationModes.OnBlank;

        millOrientGeomBuilder1.SetBlockMcsOrigin(
            NXOpen.CAM.OrientGeomBuilder.McsZeroFace.Top,
            NXOpen.CAM.OrientGeomBuilder.McsZeroPosition.Center);

        millOrientGeomBuilder1.TransferClearanceBuilder.ClearanceType =
            NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        NXOpen.Point3d origin3 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d xDirection1 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Vector3d yDirection1 = new NXOpen.Vector3d(0.0, 1.0, 0.0);

        NXOpen.Xform xform1 =
            workPart.Xforms.CreateXform(
                origin3, xDirection1, yDirection1,
                NXOpen.SmartObject.UpdateOption.AfterModeling, 1.0);

        NXOpen.CartesianCoordinateSystem cartesianCoordinateSystem1 =
            workPart.CoordinateSystems.CreateCoordinateSystem(
                xform1, NXOpen.SmartObject.UpdateOption.AfterModeling);

        millOrientGeomBuilder1.Rcs = cartesianCoordinateSystem1;

        NXOpen.NXObject nXObject4 =
            millOrientGeomBuilder1.Commit();

        millOrientGeomBuilder1.Destroy();
    }


    private static List<Body> GetAllSolidBodies(Part workPart)
    {
        List<Body> result = new List<Body>();

        Component rootComponent =
            workPart.ComponentAssembly.RootComponent;

        if (rootComponent == null)
        {
            // Peca unica, sem estrutura de montagem
            foreach (Body b in workPart.Bodies)
            {
                if (b.IsSolidBody)
                    result.Add(b);
            }
        }
        else
        {
            // Montagem: varre todos os componentes recursivamente
            CollectSolidBodiesFromComponent(rootComponent, result);
        }

        return result;
    }

    private static void CollectSolidBodiesFromComponent(
        Component component,
        List<Body> result)
    {
        Part prototypePart = component.Prototype as Part;

        if (prototypePart != null)
        {
            foreach (Body b in prototypePart.Bodies)
            {
                if (b.IsSolidBody)
                    result.Add(b);
            }
        }

        foreach (Component child in component.GetChildren())
        {
            CollectSolidBodiesFromComponent(child, result);
        }
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
    }
}