// NX 2406
// Journal created by dcard on Sat Jun 20 15:19:59 2026 Eastern Summer Time
//
using System;
using NXOpen;

public class CAVITY_MILL_THREE_PLUS_TWO_FRONT
{
  public static void Run(string[] args)
  {
    NXOpen.Session theSession = NXOpen.Session.GetSession();
    NXOpen.Part workPart = theSession.Parts.Work;
    NXOpen.Part displayPart = theSession.Parts.Display;

    
    // CORRIGIDO: journal original procurava o grupo "1234" (placeholder de teste esquecido) - o nome correto/padrão do NX pra esse grupo é "NC_PROGRAM".
    NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
    NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_ROUGH"));
    NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CUTTER_D40_R1"));
    NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
    NXOpen.CAM.Operation operation1;
    operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, tool1, featureGeometry1, "mill_contour", "CAVITY_MILL", NXOpen.CAM.OperationCollection.UseDefaultName.False, "CAVITY_MILL_FRONT", "Cavity Mill");
    

    
    NXOpen.CAM.CavityMilling cavityMilling1 = ((NXOpen.CAM.CavityMilling)operation1);
    NXOpen.CAM.CavityMillingBuilder cavityMillingBuilder1;
    cavityMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(cavityMilling1);
  

    
   
    cavityMillingBuilder1.CutPattern.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.FollowPeriphery;
    
    cavityMillingBuilder1.BndStepover.PercentToolFlatBuilder.Value = 50.0;
    
    cavityMillingBuilder1.CutLevel.GlobalDepthPerCut.DistanceBuilder.Value = 2.0;
    
    cavityMillingBuilder1.CutLevel.ApplyGlobalDepthPerCut();
    
    cavityMillingBuilder1.CutParameters.CutOrder = NXOpen.CAM.CutParametersCutOrderTypes.DepthFirst;
    
    cavityMillingBuilder1.CutParameters.IpwType = NXOpen.CAM.CutParametersIpwTypes.ThreeDimension;
    
    cavityMillingBuilder1.CutParameters.TrimControl = NXOpen.CAM.CutParametersTrimControlTypes.Silhoutte;

    cavityMillingBuilder1.ToolAxisFix.ToolAxisType = NXOpen.CAM.ToolAxisFixed.Types.Fixed;
 
    
    NXOpen.Direction nullNXOpen_Direction = null;
    cavityMillingBuilder1.ToolAxisFix.Vector = nullNXOpen_Direction;
    
    NXOpen.Point3d origin7 = new NXOpen.Point3d(0.0, 0.0, 0.0);
    NXOpen.Vector3d vector2 = new NXOpen.Vector3d(0.0, -1.0, 0.0);
    NXOpen.Direction direction2;
    direction2 = workPart.Directions.CreateDirection(origin7, vector2, NXOpen.SmartObject.UpdateOption.AfterModeling);
    
    cavityMillingBuilder1.ToolAxisFix.Vector = direction2;
    
    NXOpen.SIM.KinematicConfigurator kinematicConfigurator3;
    kinematicConfigurator3 = workPart.KinematicConfigurator;
    
    NXOpen.SIM.KinematicConfigurator kinematicConfigurator4;
    kinematicConfigurator4 = workPart.KinematicConfigurator;

    cavityMillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = 2500.0;
    
    
    cavityMillingBuilder1.FeedsBuilder.FeedCutBuilder.Value = 4500.0;
    
   
    cavityMillingBuilder1.CutParameters.SmallAreaAvoidance.SmallAreaStatus = NXOpen.CAM.SmallAreaAvoidance.StatusTypes.Ignore;
    
    cavityMillingBuilder1.CutParameters.CutBelowOverhangingBlank = false;
    

    cavityMillingBuilder1.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.RampOnShape;
    
    cavityMillingBuilder1.NonCuttingBuilder.EngageClosedAreaBuilder.HelicalRampAngleBuilder.Value = 20.0;
    
   
    cavityMillingBuilder1.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
    
    cavityMillingBuilder1.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;
    
    cavityMillingBuilder1.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NXOpen.CAM.NcmTransfer.TransferTypes.PrevPlane;
    
    cavityMillingBuilder1.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;
    
    cavityMillingBuilder1.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
    
    cavityMillingBuilder1.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;
    

    
    NXOpen.NXObject nXObject1;
    nXObject1 = cavityMillingBuilder1.Commit();
    
    NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
    NXOpen.CAM.CavityMilling cavityMilling2 = ((NXOpen.CAM.CavityMilling)nXObject1);
    objects1[0] = cavityMilling2;
    workPart.CAMSetup.GenerateToolPath(objects1);
    
  
    
    NXOpen.NXObject nXObject2;
    nXObject2 = cavityMillingBuilder1.Commit();

    
    cavityMillingBuilder1.Destroy();
    
  
    
  }
  public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
