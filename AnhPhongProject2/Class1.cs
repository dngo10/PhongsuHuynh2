using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace AnhPhongProject2
{
    class CustomException : System.Exception
    {
        public CustomException() { }
        public CustomException(string message) : base(message){}
    }

    public class Class1
    {
        const string filterDictName = "ACAD_FILTER";
        const string spatialName = "SPATIAL";

        const string linkDwg = "K:\\DANIELSOFT\\PhongBlock_1.dwg";
        List<string> acceptableLayer = new List<string>() { "FRAME_POST", "FRAME_SWALL", "FRAME_BEAR" };
        const string frameBlockAboveLayer = "FRAME_POST_ABV";

        List<string> blockNames = new List<string>();
        private List<Point3d> m_pts = new List<Point3d>();
        Dictionary<string, BlockTableRecord> allBlockTableRecord = new Dictionary<string, BlockTableRecord>();
        #region Helper

        public void copyLocktableRecord()
        {
            blockNames.Clear();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            using (Database OpenDb = new Database(false, true))
            {
                OpenDb.ReadDwgFile(linkDwg, System.IO.FileShare.Read, true, "");

                ObjectIdCollection ids = new ObjectIdCollection();
                using (Transaction tr = OpenDb.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(OpenDb.BlockTableId, OpenMode.ForRead);

                    foreach (ObjectId id in bt)
                    {
                        if (id.ObjectClass.DxfName == "BLOCK_RECORD")
                        {
                            ids.Add(id);
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                            //if (btr.Name.ToLower().Contains("model_space") || btr.Name.ToLower().Contains("paper_space")) continue;
                            if (btr.Name.ToLower().Contains('*')) continue;
                            blockNames.Add(btr.Name);
                        }
                    }
                    tr.Commit();
                }
                if (ids.Count != 0)
                {
                    Database destdb = doc.Database;
                    IdMapping iMap = new IdMapping();
                    destdb.WblockCloneObjects(ids, destdb.BlockTableId, iMap, DuplicateRecordCloning.Ignore, false);
                }
            }
        }

        public void createLayer(string layer, short colorIndex)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layer))
                {
                    LayerTableRecord ltr = new LayerTableRecord();
                    ltr.Name = layer;
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                    lt.UpgradeOpen();
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                    tr.Commit();
                }
            }
        }

        public bool IsPointInPolygon(Point3d p, List<Point2d> polygon)
        {
            double minX = polygon[0].X;
            double maxX = polygon[0].X;
            double minY = polygon[0].Y;
            double maxY = polygon[0].Y;
            for (int i = 1; i < polygon.Count; i++)
            {
                Point2d q = polygon[i];
                minX = Math.Min(q.X, minX);
                maxX = Math.Max(q.X, maxX);
                minY = Math.Min(q.Y, minY);
                maxY = Math.Max(q.Y, maxY);
            }

            if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY)
            {
                return false;
            }

            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
                     p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public bool IsPointInPolygon(Point3d p, List<Point3d> polygon)
        {
            double minX = polygon[0].X;
            double maxX = polygon[0].X;
            double minY = polygon[0].Y;
            double maxY = polygon[0].Y;
            for (int i = 1; i < polygon.Count; i++)
            {
                Point3d q = polygon[i];
                minX = Math.Min(q.X, minX);
                maxX = Math.Max(q.X, maxX);
                minY = Math.Min(q.Y, minY);
                maxY = Math.Max(q.Y, maxY);
            }

            if (p.X < minX || p.X > maxX || p.Y < minY || p.Y > maxY)
            {
                return false;
            }

            // https://wrf.ecse.rpi.edu/Research/Short_Notes/pnpoly.html
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if ((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y) &&
                     p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public BlockReference CopyDynamicBlock(BlockTableRecord spaceRecord, ref BlockReference bref, BlockTableRecord br, Transaction tr)
        {
            BlockReference newbref = new BlockReference(Point3d.Origin, br.ObjectId);
            newbref.TransformBy(bref.BlockTransform);
            newbref.BlockUnit = bref.BlockUnit;
            newbref.Normal = bref.Normal;
            newbref.Layer = frameBlockAboveLayer;

            spaceRecord.UpgradeOpen();
            spaceRecord.AppendEntity(newbref);
            tr.AddNewlyCreatedDBObject(newbref, true);
            spaceRecord.DowngradeOpen();

            //newbref.Visible = false;
            //if (bref.IsDynamicBlock)
            //{
            //    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bref.BlockTableRecord, OpenMode.ForRead);
            //    foreach (ObjectId id in btr)
            //    {
            //        DBObject temp = tr.GetObject(id, OpenMode.ForRead);
            //        if (temp is AttributeDefinition)
            //        {
            //            AttributeDefinition newAttDef = (AttributeDefinition)temp;
            //            AttributeReference attref = new AttributeReference();
            //            attref.SetAttributeFromBlock((AttributeDefinition)temp, bref.BlockTransform);
            //            newbref.AttributeCollection.AppendAttribute(attref);
            //            tr.AddNewlyCreatedDBObject(attref, true);
            //        }
            //    }
            //
            //}

            for (int i = 0; i < bref.DynamicBlockReferencePropertyCollection.Count; i++)
            {

                if (!newbref.DynamicBlockReferencePropertyCollection[i].ReadOnly &&
                    !bref.DynamicBlockReferencePropertyCollection[i].ReadOnly &&
                    newbref.DynamicBlockReferencePropertyCollection[i].PropertyName == bref.DynamicBlockReferencePropertyCollection[i].PropertyName
                    )
                {
                    newbref.DynamicBlockReferencePropertyCollection[i].Value = bref.DynamicBlockReferencePropertyCollection[i].Value;
                }

            }
            return newbref;
        }

        public Line CopyLine(BlockTableRecord spaceRecord, ref Line line, Transaction tr)
        {
            Line newLine = new Line();
            newLine.StartPoint = line.StartPoint;
            newLine.EndPoint = line.EndPoint;
            newLine.Normal = line.Normal;
            //newLine.Layer = line.Layer.Split('|').Last();
            newLine.Layer = frameBlockAboveLayer;
            //newLine.Linetype = line.Linetype;
            newLine.Linetype = "SWALL";
            newLine.PlotStyleName = line.PlotStyleName;
            
            spaceRecord.UpgradeOpen();
            spaceRecord.AppendEntity(newLine);
            tr.AddNewlyCreatedDBObject(newLine, true);
            spaceRecord.DowngradeOpen();

            return newLine;
        }

        public Polyline CopyPolyline(BlockTableRecord spaceRecord, ref Polyline pline, Transaction tr)
        {
            Polyline newpLine = new Polyline();
            int max = pline.NumberOfVertices;

            for(int i = 0; i < max; i++)
            {
                newpLine.AddVertexAt(i, pline.GetPoint2dAt(i), pline.GetBulgeAt(i), pline.GetStartWidthAt(i), pline.GetEndWidthAt(i));
            }

            newpLine.Closed = pline.Closed;
            //newpLine.Layer = pline.Layer.Split('|').Last();
            newpLine.Layer = frameBlockAboveLayer;
            newpLine.Normal = pline.Normal;
            newpLine.Color = pline.Color;
            newpLine.ColorIndex = pline.ColorIndex;

            spaceRecord.UpgradeOpen();
            spaceRecord.AppendEntity(newpLine);
            tr.AddNewlyCreatedDBObject(newpLine, true);
            spaceRecord.DowngradeOpen();

            return newpLine;
        }

        public List<Point3d> createRectangleForXclip(List<Point3d> boundary)
        {
            if (boundary.Count != 2) return boundary;

            Point3d point2 = new Point3d(boundary[1].X, boundary[0].Y, 0);
            Point3d point4 = new Point3d(boundary[0].X, boundary[1].Y, 0);

            if ((boundary[0].X < boundary[1].X && boundary[0].Y < boundary[1].Y) ||
                (boundary[0].X > boundary[1].X && boundary[0].Y > boundary[1].Y))
            {
                boundary.Insert(1, point2);
                boundary.Insert(3, point4);
            }
            else if (
              (boundary[0].X < boundary[1].X && boundary[0].Y > boundary[1].Y) ||
              (boundary[0].X > boundary[1].X && boundary[0].Y < boundary[1].Y)
              )
            {
                boundary.Insert(1, point4);
                boundary.Insert(3, point2);
            }
            return boundary;
        }

        public List<Point2d> createRectangleForXclip(List<Point2d> boundary)
        {
            if (boundary.Count != 2) return boundary;

            Point2d point2 = new Point2d(boundary[1].X, boundary[0].Y);
            Point2d point4 = new Point2d(boundary[0].X, boundary[1].Y);

            if ((boundary[0].X < boundary[1].X && boundary[0].Y < boundary[1].Y) ||
                (boundary[0].X > boundary[1].X && boundary[0].Y > boundary[1].Y))
            {
                boundary.Insert(1, point2);
                boundary.Insert(3, point4);
            }
            else if (
              (boundary[0].X < boundary[1].X && boundary[0].Y > boundary[1].Y) ||
              (boundary[0].X > boundary[1].X && boundary[0].Y < boundary[1].Y)
              )
            {
                boundary.Insert(1, point4);
                boundary.Insert(3, point2);
            }
            return boundary;
        }

        public bool canAdd(bool isInverted, List<Point2d> boundary, Point3d position)
        {
            if (boundary.Count == 0 || boundary.Count == 1) return true;
            if (boundary.Count == 2)
            {
                boundary = createRectangleForXclip(boundary);
            }

            if (isInverted)
            {
                return !IsPointInPolygon(position, boundary);
            }
            else
            {
                return IsPointInPolygon(position, boundary);
            }

        }

        public bool canAdd(bool isInverted, List<Point3d> boundary, Point3d position)
        {
            if (boundary.Count == 0 || boundary.Count == 1) return true;
            if (boundary.Count == 2)
            {
                boundary = createRectangleForXclip(boundary);
            }

            if (isInverted)
            {
                return !IsPointInPolygon(position, boundary);
            }
            else
            {
                return IsPointInPolygon(position, boundary);
            }

        }

        public bool canAddPolyline(bool isInverted, List<Point2d> boundary, Polyline pline)
        {
            if (boundary.Count == 0 || boundary.Count == 1) return true;
            if (boundary.Count == 2)
            {
                boundary = createRectangleForXclip(boundary);
            }
            int numberofVertex = pline.NumberOfVertices;
            if (numberofVertex == 0) return false;
            int inSideTime = 0;

            for(int i = 0; i < numberofVertex; i++)
            {
                bool ans;
                Point3d point = pline.GetPoint3dAt(i);
                if (isInverted)
                {
                    ans = !IsPointInPolygon(point, boundary);
                }
                else
                {
                    ans = IsPointInPolygon(point, boundary);
                }

                if (ans) inSideTime++;
            }

            if (inSideTime == 0) return false;
            return (double)inSideTime / numberofVertex >= 0.5;

        }

        public bool canAddPolyline(bool isInverted, List<Point3d> boundary, Polyline pline)
        {
            if (boundary.Count == 0 || boundary.Count == 1) return true;
            if (boundary.Count == 2)
            {
                boundary = createRectangleForXclip(boundary);
            }
            int numberofVertex = pline.NumberOfVertices;
            if (numberofVertex == 0) return false;
            int inSideTime = 0;

            for (int i = 0; i < numberofVertex; i++)
            {
                bool ans;
                Point3d point = pline.GetPoint3dAt(i);
                if (isInverted)
                {
                    ans = !IsPointInPolygon(point, boundary);
                }
                else
                {
                    ans = IsPointInPolygon(point, boundary);
                }

                if (ans) inSideTime++;
            }

            if (inSideTime == 0) return false;
            return (double)inSideTime / numberofVertex >= 0.5;
        }

        bool getSelectedWindow(ref List<Point3d> boundary, ref Editor ed, ref Database db)
        {

            PromptSelectionOptions pso = new PromptSelectionOptions();
            pso.MessageForAdding = "Select Objects: ";
            pso.SingleOnly = false;
            PromptSelectionResult psr = ed.GetSelection(pso);
            if (psr.Status != PromptStatus.OK) return false;
            SelectionSetDelayMarshalled ssMarshal = (SelectionSetDelayMarshalled)psr.Value;
            AdsName name = ssMarshal.Name;
            SelectionSet selSet = (SelectionSet)ssMarshal;
            foreach (SelectedObject item in selSet)
            {
                boundary.Clear();
                switch (item.SelectionMethod)
                {
                    case SelectionMethod.Crossing:
                        CrossingOrWindowSelectedObject crossSelObj = item as CrossingOrWindowSelectedObject;
                        PickPointDescriptor[] crossSelPickedPoints = crossSelObj.GetPickPoints();
                        foreach (PickPointDescriptor point in crossSelPickedPoints) boundary.Add(point.PointOnLine);
                        break;
                    case SelectionMethod.Window:
                        CrossingOrWindowSelectedObject windSelObj = item as CrossingOrWindowSelectedObject;
                        PickPointDescriptor[] winSelPickedPoints = windSelObj.GetPickPoints();
                        foreach (PickPointDescriptor point in winSelPickedPoints) boundary.Add(point.PointOnLine);
                        break;
                }
            }
            return true;
        }

        public Dictionary<string, BlockTableRecord> getBlockTableRecord(BlockTable bt, Transaction tr)
        {
            Dictionary<string, BlockTableRecord> ans = new Dictionary<string, BlockTableRecord>();

            foreach(string blockName in blockNames)
            {
                if (bt.Has(blockName))
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                    ans.Add(blockName, btr);
                }
            }

            return ans;
        }

        public BlockReference createAnonymousBlock(BlockTable bt, List<Entity> entities, BlockTableRecord spaceRecord, Transaction tr, Database db)
        {
            BlockTableRecord btr = new BlockTableRecord();
            spaceRecord.UpgradeOpen();
            ObjectId btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);
            

            ObjectIdCollection obIdCol = new ObjectIdCollection();

            foreach(Entity ent in entities)
            {
                //ent.TransformBy(matrix);
                obIdCol.Add(ent.ObjectId);

            }

            IdMapping mapping = new IdMapping();
            db.DeepCloneObjects(obIdCol, btrId, mapping, false);

            BlockReference bref = new BlockReference(Point3d.Origin, btrId);
            spaceRecord.AppendEntity(bref);
            tr.AddNewlyCreatedDBObject(bref, true);
            spaceRecord.DowngradeOpen();

            foreach(Entity ent in entities)
            {
                ent.Erase();
            }
            return bref;
        }

        public List<Entity> MergeEntity(ref List<Line> lines, ref List<Polyline> plines, ref List<BlockReference> blockReferences, BlockTableRecord spaceRecord, Transaction tr)
        {
            List<Entity> ans = new List<Entity>();
            ans.AddRange(lines);
            ans.AddRange(plines);
            for(int i = 0; i < blockReferences.Count; i++)
            {
                ans.AddRange(exploreBlock(ref spaceRecord, blockReferences[i], ref tr));
            }

            return ans;
        }

        public List<Entity> exploreBlock(ref BlockTableRecord spaceRecord, BlockReference block, ref Transaction tr)
        {
            List<Entity> ans = new List<Entity>();
            using(DBObjectCollection dbObjCol = new DBObjectCollection())
            {
                block.Explode(dbObjCol);
                
                foreach(DBObject dbObj in dbObjCol)
                {
                    Entity acEnt = dbObj as Entity;
                    acEnt.Layer = frameBlockAboveLayer;
                    spaceRecord.AppendEntity(acEnt);
                    tr.AddNewlyCreatedDBObject(acEnt, true);
                
                    ans.Add(acEnt);
                }
            }
            block.Erase();
            return ans;
        }

        #endregion

        public HashSet<ObjectId> EditorSelectCrossingWindow(Editor editor, List<Point3d> pts)
        {
            TypedValue[] filterList = new TypedValue[1];
            filterList[0] = new TypedValue(0, "INSERT,LINE,LWPOLYLINE");
            SelectionFilter filter = new SelectionFilter(filterList);

            HashSet<ObjectId> choosenIds = new HashSet<ObjectId>();
            PromptSelectionResult selRes = editor.SelectCrossingWindow(pts[0], pts[2], filter);

            if (selRes.Status == PromptStatus.OK)
            {
                foreach (ObjectId id in selRes.Value.GetObjectIds())
                {
                    choosenIds.Add(id);
                }
            }

            PromptSelectionResult selResReversed = editor.SelectCrossingWindow(pts[2], pts[0], filter);

            if (selResReversed.Status == PromptStatus.OK)
            {
                foreach (ObjectId id in selResReversed.Value.GetObjectIds())
                {
                    choosenIds.Add(id);
                }
            }
            return choosenIds;
        }

        public void CheckBoundaryAndInverted(ref BlockReference bref, Transaction tr, ref List<Point2d> boundary, ref bool hasBoundary, ref bool isInverted)
        {
            if (!bref.ExtensionDictionary.IsNull)
            {
                DBDictionary extDict = (DBDictionary)tr.GetObject(bref.ExtensionDictionary, OpenMode.ForRead);
                if (extDict != null && extDict.Contains(filterDictName))
                {
                    DBDictionary fildict = (DBDictionary)tr.GetObject(extDict.GetAt(filterDictName), OpenMode.ForRead);
                    if (fildict != null)
                    {
                        if (fildict.Contains(spatialName))
                        {
                            var fil = (SpatialFilter)tr.GetObject(fildict.GetAt(spatialName), OpenMode.ForRead);
                            if (fil != null)
                            {
                                Extents3d ext = fil.GetQueryBounds();
                                isInverted = fil.Inverted;
                                var pts = fil.Definition.GetPoints();

                                //Matrix3d inverseMatrix = brefMatrix.Inverse();
                                foreach (var pt in pts)
                                {
                                    Point3d point3 = new Point3d(pt.X, pt.Y, 0);
                                    point3 = point3.TransformBy(fil.OriginalInverseBlockTransform);
                                    boundary.Add(new Point2d(point3.X, point3.Y));
                                    //ed.WriteMessage("\nBoundary point at {0}", pt);
                                }
                            }
                        }
                        if (boundary.Count >= 2)
                        {
                            hasBoundary = true;
                        }
                    }
                }
            }
        }

        public void RunCode(List<Point3d> pts, Transaction tr, ref Database db, Editor ed, ref List<BlockReference> InBoundaryAddBrefs, ref List<Line> InBoundaryLines, ref List<Polyline> InBoundaryPlines, ref HashSet<String> NothingBlocks)
        {
            //Setup layer and blockTableRecord
            foreach(string layer in acceptableLayer)
            {
                createLayer(layer, 1);
            }
            
            copyLocktableRecord();

            List<BlockReference> sWallBlocks = new List<BlockReference>();
            List<Line> sWallLines = new List<Line>();
            List<Polyline> sWallPlines = new List<Polyline>();

            HashSet<ObjectId> choosenIds = EditorSelectCrossingWindow(ed, pts);
            if (choosenIds.Count == 0) return;


            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord spaceRecord = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

            //GetAllBlockName
            allBlockTableRecord.Clear();
            allBlockTableRecord = getBlockTableRecord(bt, tr);

            foreach (ObjectId objectId in choosenIds)
            {
                DBObject dbObject = tr.GetObject(objectId, OpenMode.ForRead);


                if (objectId.ObjectClass == RXObject.GetClass(typeof(Line)))
                {
                    Line line = dbObject as Line;
                    if (acceptableLayer.Contains(line.Layer.Split('|').Last()))
                    {
                        sWallLines.Add(CopyLine(spaceRecord, ref line, tr));
                    }
                }
                else if(dbObject is Polyline)
                {
                    Polyline pline = dbObject as Polyline;
                    if (acceptableLayer.Contains(pline.Layer.Split('|').Last()))
                    {
                        sWallPlines.Add(CopyPolyline(spaceRecord, ref pline, tr));
                    }
                }

                else if (dbObject is BlockReference)
                {
                    BlockReference bref = dbObject as BlockReference;
                    BlockTableRecord brefRecord = (BlockTableRecord)tr.GetObject(bref.BlockTableRecord, OpenMode.ForRead);

                    string brefName = bref.IsDynamicBlock ? ((BlockTableRecord)bref.DynamicBlockTableRecord.GetObject(OpenMode.ForRead)).Name : bref.Name;

                    if (allBlockTableRecord.Keys.Contains(brefName) && acceptableLayer.Contains(bref.Layer.Split('|').Last()))
                    {
                        sWallBlocks.Add(CopyDynamicBlock(spaceRecord, ref bref, allBlockTableRecord[brefName], tr));
                    }
                    else
                    {
                        List<BlockReference> rawNewBlocks = new List<BlockReference>();
                        List<Line> newLines = new List<Line>();
                        List<Polyline> newPLines = new List<Polyline>();

                        InspectBlockReference(ref tr, ref spaceRecord, ref bref, ref rawNewBlocks, ref newLines, ref newPLines, ref NothingBlocks);
                        sWallBlocks.AddRange(rawNewBlocks);
                        sWallLines.AddRange(newLines);
                        sWallPlines.AddRange(newPLines);
                    }
                }
            }


            List<Point3d> boundary = createRectangleForXclip(pts);
            foreach (BlockReference bref in sWallBlocks)
            {
                if (canAdd(false, boundary, bref.Position))
                {
                    InBoundaryAddBrefs.Add(bref);
                }
                else
                {
                    bref.Erase();
                }
            }

            foreach (Line line in sWallLines)
            {
                if (canAdd(false, boundary, line.StartPoint) || canAdd(false, boundary, line.EndPoint))
                {
                    InBoundaryLines.Add(line);
                }
                else
                {
                    line.Erase();
                }
            }

            foreach(Polyline pline in sWallPlines)
            {
                if(canAddPolyline(false, boundary, pline))
                {
                    InBoundaryPlines.Add(pline);
                }
                else
                {
                    pline.Erase();
                }
            }

            return;
        }

        //The lines, plines, and blocks MUST BE EMPTY when this function is called.
        public void InspectBlockReference(ref Transaction tr, ref BlockTableRecord spaceRecord,
                                          ref BlockReference bref, ref List<BlockReference> blocks,
                                          ref List<Line> lines, ref List<Polyline> plines, ref HashSet<string> NothingBlocks)
        {
            List<Point2d> boundary = new List<Point2d>();
            bool isInverted = false;
            bool hasBoundary = false;

            if (bref != null)
            {
                CheckBoundaryAndInverted(ref bref, tr, ref boundary, ref hasBoundary, ref isInverted);

                BlockTableRecord newBtr = (BlockTableRecord)tr.GetObject(bref.BlockTableRecord, OpenMode.ForRead);

                foreach (ObjectId id in newBtr)
                {
                    if (id.ObjectClass.DxfName == "INSERT")
                    {
                        BlockReference newBref1 = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                        Matrix3d matrix3d = newBref1.BlockTransform;
                        string brefName = newBref1.IsDynamicBlock ? ((BlockTableRecord)newBref1.DynamicBlockTableRecord.GetObject(OpenMode.ForRead)).Name : newBref1.Name;

                        //Remove prefix -- File, Path, etc...
                        brefName = brefName.Split('|').Last();

                        if (allBlockTableRecord.Keys.Contains(brefName) && !NothingBlocks.Contains(brefName) && acceptableLayer.Contains(newBref1.Layer.Split('|').Last()))
                        {
                            BlockReference bref2 = CopyDynamicBlock(spaceRecord, ref newBref1, allBlockTableRecord[brefName], tr);
                            if (!hasBoundary || canAdd(isInverted, boundary, bref2.Position))
                            {
                                blocks.Add(bref2);
                            }
                            else
                            {
                                bref2.Erase();
                            }
                        }
                        else
                        {
                            List<BlockReference> newObjects = new List<BlockReference>();
                            List<Line> newLines = new List<Line>();
                            List<Polyline> newPlines = new List<Polyline>();

                            InspectBlockReference(ref tr, ref spaceRecord, ref newBref1, ref newObjects, ref newLines, ref newPlines, ref NothingBlocks);

                            if (newObjects.Count == 0 && newLines.Count == 0 && newPlines.Count == 0)
                            {
                                NothingBlocks.Add(brefName);
                            }
                            if (hasBoundary)
                            {
                                foreach (BlockReference nbref in newObjects)
                                {
                                    if (canAdd(isInverted, boundary, nbref.Position))
                                    {
                                        blocks.Add(nbref);
                                    }
                                    else
                                    {
                                        nbref.Erase();
                                    }
                                }

                                foreach(Line line in newLines)
                                {
                                    if(canAdd(isInverted, boundary, line.StartPoint) || canAdd(isInverted, boundary, line.EndPoint))
                                    {
                                        lines.Add(line);
                                    }
                                    else
                                    {
                                        line.Erase();
                                    }
                                }
                                foreach(Polyline pline in newPlines)
                                {
                                    if(canAddPolyline(isInverted, boundary, pline))
                                    {
                                        plines.Add(pline);
                                    }
                                    else
                                    {
                                        pline.Erase();
                                    }
                                }
                            }
                            else
                            {
                                blocks.AddRange(newObjects);
                                lines.AddRange(newLines);
                                plines.AddRange(newPlines);
                            }
                        }
                    }
                    else if (id.ObjectClass == RXObject.GetClass(typeof(Line)))
                    {
                        Line line = (Line)tr.GetObject(id, OpenMode.ForRead);
                        if (acceptableLayer.Contains(line.Layer.Split('|').Last()))
                        {
                            Line newLine = CopyLine(spaceRecord, ref line, tr);
                            if (canAdd(isInverted, boundary, newLine.StartPoint) || canAdd(isInverted, boundary, newLine.EndPoint))
                            {
                                lines.Add(newLine);
                            }
                            else
                            {
                                newLine.Erase();
                            }
                        }
                    } else if(id.ObjectClass == RXObject.GetClass(typeof(Polyline)))
                    {
                        Polyline pline = (Polyline)tr.GetObject(id, OpenMode.ForRead);
                        if (acceptableLayer.Contains(pline.Layer.Split('|').Last()))
                        {
                            Polyline newPline = CopyPolyline(spaceRecord, ref pline, tr);
                            if(canAddPolyline(isInverted, boundary, newPline))
                            {
                                plines.Add(newPline);
                            }
                            else
                            {
                                newPline.Erase();
                            }
                            
                        }
                    }
                }

                for (int i = 0; i < blocks.Count; i++)
                {
                    blocks[i].TransformBy(bref.BlockTransform);
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i].TransformBy(bref.BlockTransform);
                }

                for(int i = 0; i < plines.Count; i++)
                {
                    plines[i].TransformBy(bref.BlockTransform);
                }
            }
        }

        
        public void CheckLinetype()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            using(Transaction tr = db.TransactionManager.StartTransaction())
            {
                LinetypeTable ltt = tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                //foreach(ObjectId id in ltt)
                //{
                //    LinetypeTableRecord lttr = (LinetypeTableRecord)tr.GetObject(id, OpenMode.ForRead);
                //}
                if (!ltt.Has("SWALL"))
                {
                    LinetypeTableRecord lttr = new LinetypeTableRecord();
                    lttr.Name = "SWALL";
                    lttr.PatternLength = 0.75;
                    lttr.NumDashes = 3;
                    lttr.Comments = "___________//_______";
                    lttr.AsciiDescription = "___________//_______";

                    ltt.UpgradeOpen();
                    ltt.Add(lttr);
                    tr.AddNewlyCreatedDBObject(lttr, true);
                    tr.Commit();
                }
            }


        }

        [CommandMethod("PhongSuHuynh")]
        public void BoxJig()
        {
            CheckLinetype();
            createLayer(frameBlockAboveLayer, 1);
            m_pts.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            if (!getSelectedWindow(ref m_pts, ref ed, ref db))
            {
                return;
            }

            using (doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    List<BlockReference> blocks = new List<BlockReference>();
                    List<Line> lines = new List<Line>();
                    List<Polyline> plines = new List<Polyline>();

                    HashSet<string> NothingBlocks = new HashSet<string>();

                    //RunCode(rec.firstPoint, rec.secondPoint, tr, ref db, ed, ref blocks, ref lines);
                    RunCode(m_pts, tr, ref db, ed, ref blocks, ref lines, ref plines, ref NothingBlocks);


                    BlockReference bref = createAnonymousBlock(bt, MergeEntity(ref lines, ref plines, ref blocks, btr, tr), btr, tr, db);
                    bref.Layer = frameBlockAboveLayer;
                    if (blocks.Count != 0 || lines.Count != 0 || plines.Count != 0)
                    {

                        var ppr2 = ed.GetPoint("\nSpecify base point: ");
                        
                        if (ppr2.Status == PromptStatus.OK)
                        {
                            DragBref dragBref = new DragBref(bref, ppr2.Value);
                            PromptResult result = ed.Drag(dragBref);
                            if (result.Status == PromptStatus.Cancel) bref.Erase();
                        }
                        //foreach (var entity in lines) entity.Erase();
                        //foreach (var entity in plines) entity.Erase();
                        //foreach (var entity in blocks) entity.Erase();
                    }
                    tr.Commit();

                }
            }  
        }


    }

    class RecJig : DrawJig
    {
        public Point3d firstPoint;
        public Point3d secondPoint;
        public List<Point3d> primaryBoundary = new List<Point3d>();
        //THIS IS HOW YOU MAKE IT DONE

        public RecJig(Point3d firstPoint)
        {
            this.firstPoint = firstPoint;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {

            JigPromptPointOptions jigOpts = new JigPromptPointOptions();
            jigOpts.UserInputControls = (
                UserInputControls.Accept3dCoordinates |
                UserInputControls.UseBasePointElevation
                );

            jigOpts.Message = "\nSpecify second point: ";

            PromptPointResult promptPointResult = prompts.AcquirePoint(jigOpts);
            if (promptPointResult.Status == PromptStatus.OK)
            {
                secondPoint = promptPointResult.Value;
                return SamplerStatus.OK;
            }
            else
            {
                return SamplerStatus.Cancel;
            }
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            WorldGeometry geo = draw.Geometry;
            List<Point3d> points = new List<Point3d>();
            points.Add(firstPoint);
            points.Add(secondPoint);
            primaryBoundary = createRectangleForXclip(points);

            if (primaryBoundary == null || primaryBoundary.Count != 4) return false;

            geo.WorldLine(primaryBoundary[0], primaryBoundary[1]);
            geo.WorldLine(primaryBoundary[1], primaryBoundary[2]);
            geo.WorldLine(primaryBoundary[2], primaryBoundary[3]);
            geo.WorldLine(primaryBoundary[3], primaryBoundary[0]);

            return true;
        }

        public List<Point3d> createRectangleForXclip(List<Point3d> boundary)
        {
            if (boundary.Count != 2) return null;

            Point3d point2 = new Point3d(boundary[1].X, boundary[0].Y, 0);
            Point3d point4 = new Point3d(boundary[0].X, boundary[1].Y, 0);

            //Point2d point2_1 = new Point2d(boundary[0].X, boundary[1].Y);
            //Point2d point4_1 = new Point2d(boundary[1].X, boundary[0].Y);

            if ((boundary[0].X < boundary[1].X && boundary[0].Y < boundary[1].Y) ||
                (boundary[0].X > boundary[1].X && boundary[0].Y > boundary[1].Y))
            {
                boundary.Insert(1, point2);
                boundary.Insert(3, point4);
            }
            else if (
              (boundary[0].X < boundary[1].X && boundary[0].Y > boundary[1].Y) ||
              (boundary[0].X > boundary[1].X && boundary[0].Y < boundary[1].Y)
              )
            {
                boundary.Insert(1, point4);
                boundary.Insert(3, point2);
            }
            return boundary;
        }
    }

    class DragBref : DrawJig
    {
        BlockReference bref;
        Point3d basePoint;
        public Point3d secondPoint;
        Point3d brefPosition;

        public DragBref(BlockReference bref, Point3d basePoint)
        {
            this.bref = bref;
            this.basePoint = basePoint;
            brefPosition = bref.Position;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions pointOp = new JigPromptPointOptions("\nSpecify second point: ");
            pointOp.UserInputControls = (
                UserInputControls.Accept3dCoordinates |
                UserInputControls.UseBasePointElevation
                );

            pointOp.BasePoint = basePoint;
            pointOp.UseBasePoint = true;

            PromptPointResult result = prompts.AcquirePoint(pointOp);
            if (result.Status == PromptStatus.OK)
            {
                secondPoint = result.Value;
                return SamplerStatus.OK;
            }
            else return SamplerStatus.Cancel;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            WorldGeometry geo = draw.Geometry;
            if (geo != null)
            {
                Vector3d vec = basePoint.GetVectorTo(secondPoint);
                Matrix3d disp = Matrix3d.Displacement(this.basePoint.GetVectorTo(this.secondPoint));
                //geo.PushModelTransform(disp);

                Point3d tempPoint = brefPosition + vec;
                bref.Position = tempPoint;
                geo.Draw(bref);
            }
            return true;
        }
    }

    class DragEntities : DrawJig
    {
        List<BlockReference> brefs = new List<BlockReference>();
        List<Line> lines = new List<Line>();
        List<Autodesk.AutoCAD.DatabaseServices.Polyline> plines = new List<Autodesk.AutoCAD.DatabaseServices.Polyline>();

        List<Point3d> blockPosition = new List<Point3d>();
        List<Tuple<Point3d, Point3d>> linesPosition = new List<Tuple<Point3d, Point3d>>();
        List<List<Point3d>> plinesPosition = new List<List<Point3d>>();
        
        Point3d basePoint;
        Point3d secondPoint;

        public DragEntities(List<BlockReference> brefs, List<Line> lines, List<Autodesk.AutoCAD.DatabaseServices.Polyline> plines, Point3d basePoint)
        {
            this.basePoint = basePoint;
            this.brefs = brefs;
            this.lines = lines;
            this.plines = plines;

            foreach (BlockReference bref in brefs)
            {
                blockPosition.Add(new Point3d(bref.Position.X, bref.Position.Y, 0));
            }
            foreach (Line line in lines)
            {
                linesPosition.Add(new Tuple<Point3d, Point3d>(line.StartPoint, line.EndPoint));
            }

            foreach(Autodesk.AutoCAD.DatabaseServices.Polyline pline in plines)
            {
                List<Point3d> plineL = new List<Point3d>();
                for(int i = 0; i < pline.NumberOfVertices; i++)
                {
                    plineL.Add(pline.GetPoint3dAt(i));
                }
                plinesPosition.Add(plineL);
            }
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions pointOp = new JigPromptPointOptions("\nSpecify second point: ");
            pointOp.UserInputControls = (
                UserInputControls.Accept3dCoordinates |
                UserInputControls.UseBasePointElevation
                );

            pointOp.BasePoint = basePoint;
            pointOp.UseBasePoint = true;

            PromptPointResult result = prompts.AcquirePoint(pointOp);
            if (result.Status == PromptStatus.OK)
            {
                secondPoint = result.Value;
                return SamplerStatus.OK;
            }
            else return SamplerStatus.Cancel;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            WorldGeometry geo = draw.Geometry;
            if (geo != null)
            {
                Vector3d vec = basePoint.GetVectorTo(secondPoint);
                Matrix3d disp = Matrix3d.Displacement(this.basePoint.GetVectorTo(this.secondPoint));
                //geo.PushModelTransform(disp);
                for (int i = 0; i < blockPosition.Count; i++)
                {
                    Point3d tempPoint = blockPosition[i] + vec;
                    brefs[i].Position = tempPoint;
                    geo.Draw(brefs[i]);
                }

                for (int i = 0; i < linesPosition.Count; i++)
                {
                    Point3d tempStartPoint = linesPosition[i].Item1 + vec;
                    Point3d tempEndPoint = linesPosition[i].Item2 + vec;

                    lines[i].StartPoint = tempStartPoint;
                    lines[i].EndPoint = tempEndPoint;
                    geo.Draw(lines[i]);
                }

                if(plinesPosition.Count != 0)
                {
                    for (int i = 0; i < plinesPosition.Count; i++)
                    {
                        List<Point3d> tempVertext = new List<Point3d>();
                        for (int j = 0; j < plinesPosition[j].Count; j++)
                        {
                            Point3d newVert = plinesPosition[i][j] + vec;
                            double bulge = plines[i].GetBulgeAt(j);
                            double startWidth = plines[i].GetStartWidthAt(j);
                            double endWidth = plines[i].GetEndWidthAt(j);
                            plines[i].RemoveVertexAt(j);
                            plines[i].AddVertexAt(j, new Point2d(newVert.X, newVert.Y), bulge, startWidth, endWidth);
                        }
                        geo.Draw(plines[i]);
                    }
                }


                //geo.PopModelTransform();
                return true;
            }
            return false;
        }
    }

}
