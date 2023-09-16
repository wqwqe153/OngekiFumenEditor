﻿using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    internal class ConnectableObjectInteractiveAction : DefaultObjectInteractiveAction
    {
        private struct DragInfo
        {
            public ILaneDockable Dockable { get; set; }

            public XGrid XGrid { get; set; }
            public TGrid TGrid { get; set; }

            public LaneStartBase RefLane { get; set; }
        }

        private Dictionary<OngekiObjectBase, HashSet<DragInfo>> dragInfoMap = new();

        public override void OnMoveCanvas(OngekiObjectBase o, Point point, FumenVisualEditorViewModel editor)
        {
            base.OnMoveCanvas(o, point, editor);
            var obj = o switch
            {
                ConnectableObjectBase co => co,
                LaneCurvePathControlObject ctrl => ctrl.RefCurveObject,
                _ => default
            };
            RelocateDockableObjects(editor, obj);
        }

        public override void OnDragStart(OngekiObjectBase o, Point pos, FumenVisualEditorViewModel editor)
        {
            base.OnDragStart(o, pos, editor);

            var obj = o switch
            {
                ConnectableObjectBase co => co,
                LaneCurvePathControlObject ctrl => ctrl.RefCurveObject,
                _ => default
            };

            var start = obj switch
            {
                ConnectableChildObjectBase c => c.ReferenceStartObject,
                ConnectableStartObject s => s,
                _ => default
            };

            var refLaneId = obj.RecordId;

            var minTGrid = obj.TGrid;
            var maxTGrid = obj.NextObject?.TGrid ?? minTGrid;
            if (obj is ConnectableChildObjectBase child)
                minTGrid = child.PrevObject.TGrid;

            var infoList = editor.Fumen.GetAllDisplayableObjects(minTGrid, maxTGrid)
                .OfType<ILaneDockable>()
                .Where(x => x.ReferenceLaneStrId == refLaneId)
                .Where(x => !((ISelectableObject)x).IsSelected)
                .Select(x =>
                {
                    var info = new DragInfo();
                    info.Dockable = x;

                    if (x is IHorizonPositionObject horizonPositionObject)
                        info.XGrid = horizonPositionObject.XGrid.CopyNew();

                    if (x is ITimelineObject timelineObject)
                        info.TGrid = timelineObject.TGrid.CopyNew();

                    info.RefLane = info.Dockable.ReferenceLaneStart;

                    return info;
                })
                .ToHashSet();

            dragInfoMap[o] = infoList;
        }

        public override void OnDragEnd(OngekiObjectBase o, Point point, FumenVisualEditorViewModel editor)
        {
            base.OnDragEnd(o, point, editor);

            var obj = o switch
            {
                ConnectableObjectBase co => co,
                LaneCurvePathControlObject ctrl => ctrl.RefCurveObject,
                _ => default
            };

            if (dragInfoMap.TryGetValue(o, out var infoList))
                dragInfoMap.Remove(o);
            else
                return;//YOU SHOULD NOT BE HERE

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("附着物件自动更新水平位置",
                () =>
                {
                    RelocateDockableObjects(editor, obj);
                }, () =>
                {
                    foreach (var info in infoList)
                    {
                        info.Dockable.XGrid = info.XGrid;
                        info.Dockable.TGrid = info.TGrid;
                        info.Dockable.ReferenceLaneStart = info.RefLane;
                    }
                }));
        }

        private void RelocateDockableObjects(FumenVisualEditorViewModel editor, OngekiObjectBase obj)
        {
            var connectable = (ConnectableObjectBase)obj;
            var start = connectable switch
            {
                ConnectableChildObjectBase c => c.ReferenceStartObject,
                ConnectableStartObject s => s,
                _ => default
            };

            RelocateDockableObjects(editor, connectable, start);
            if (connectable is ConnectableChildObjectBase child)
                RelocateDockableObjects(editor, child.PrevObject, start);
        }

        private void RelocateDockableObjects(FumenVisualEditorViewModel editor, ConnectableObjectBase obj, ConnectableStartObject start)
        {
            if (obj.NextObject is null)
                return;
            var refLaneId = obj.RecordId;

            var minTGrid = obj.TGrid;
            var maxTGrid = obj.NextObject.TGrid;

            using var _ = editor.Fumen.GetAllDisplayableObjects(minTGrid, maxTGrid)
                .OfType<ILaneDockable>()
                .Where(x => x.ReferenceLaneStrId == refLaneId)
                .Where(x => !((ISelectableObject)x).IsSelected)
                .ToHashSetWithObjectPool(out var dockables);

            foreach (var dockable in dockables)
            {
                if (start.CalulateXGrid(dockable.TGrid) is XGrid xGrid)
                    dockable.XGrid = xGrid;

                if (dockable is Hold hold && hold.HoldEnd is HoldEnd end)
                {
                    if (end.RefHold?.ReferenceLaneStart?.CalulateXGrid(end.TGrid) is XGrid xGrid2)
                        end.XGrid = xGrid2;
                }
            }
        }
    }
}
