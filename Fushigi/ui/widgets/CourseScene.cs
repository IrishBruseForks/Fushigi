﻿using Fushigi.Byml;
using Fushigi.course;
using Fushigi.param;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using Silk.NET.Input;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using System.Diagnostics;
using System.Drawing;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Xml.Linq;

namespace Fushigi.ui.widgets
{
    class CourseScene
    {

        LevelViewport viewport;
        readonly Course course;
        CourseArea selectedArea;

        readonly Dictionary<string, bool> mLayersVisibility = [];
        bool mHasFilledLayers = false;
        bool mAllLayersVisible = true;
        bool mShowAddActor = false;

        CourseActor? mSelectedActor = null;

        public CourseScene(Course course)
        {
            this.course = course;
            selectedArea = course.GetArea(0);
            viewport = new LevelViewport(selectedArea);
        }

        public void DrawUI()
        {
            bool status = ImGui.Begin("Course");

            CourseTabBar();

            viewport.Draw(ImGui.GetContentRegionAvail(), mLayersVisibility, 
                selectedActors: new HashSet<BymlHashTable>()); //only temporary

            AreaParameterPanel();

            ActorsPanel();

            ActorParameterPanel();

            if (mShowAddActor)
            {
                SelectActor();
            }

            if (viewport.HasSelectionChanged())
            {
                CourseActor selectedActor = viewport.GetSelectedActors().ElementAt(0);
                mSelectedActor = selectedActor;
            }

            if (status)
            {
                ImGui.End();
            }
        }

        public void Save()
        {
            //Save each course area to current romfs folder
            foreach (var area in this.course.GetAreas())
                area.Save();
        }

        public void Save(string folder)
        {
            //Save each course area to a specific folder
            foreach (var area in this.course.GetAreas())
                area.Save(folder);
        }

        private void CourseTabBar()
        {
            bool tabStatus = ImGui.BeginTabBar("Courses TabBar"); // Not sure what the string argument is for

            foreach (var area in course.GetAreas())
            {
                if (ImGui.BeginTabItem(area.GetName()))
                {
                    // Tab change
                    if (selectedArea != area)
                    {
                        selectedArea = area;
                        viewport = new(area);

                        // Unselect actor
                        // This is so that users do not see an actor selected from another area
                        mSelectedActor = null;
                    }

                    ImGui.EndTabItem();
                }
            }

            if (tabStatus)
            {
                ImGui.EndTabBar();
            }
        }

        private void SelectActor()
        {
            bool status = ImGui.Begin("Add Actor");

            ImGui.BeginListBox("Select the actor you want to add.", ImGui.GetContentRegionAvail());

            foreach (string actor in ParamDB.GetActors())
            {
                ImGui.Selectable(actor);

                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                {
                    viewport.mEditorState = LevelViewport.EditorState.AddingActor;
                    viewport.mActorToAdd = actor;
                    mShowAddActor = false;
                }
            }

            ImGui.EndListBox();

            if (status)
            {
                ImGui.End();
            }
        }

        private void ActorsPanel()
        {
            ImGui.Begin("Actors");

            if (ImGui.Button("Add Actor"))
            {
                mShowAddActor = true;
            }

            if (ImGui.Button("Delete Actor"))
            {
                viewport.mEditorState = LevelViewport.EditorState.DeletingActor;
            }

            // actors are in an array
            CourseActorHolder actorArray = selectedArea.mActorHolder;

            //CourseActorsTreeView(actorArray);
            CourseActorsLayerView(actorArray);

            ImGui.End();
        }

        private void ActorParameterPanel()
        {
            bool status = ImGui.Begin("Actor Parameters");

            if (mSelectedActor is null)
            {
                ImGui.Text("No Actor is selected");
            }
            else
            {
                string actorName = mSelectedActor.mActorName;
                string name = mSelectedActor.mName;

                ImGui.Text(actorName);

                ImGui.Separator();

                ImGui.Columns(2);

                ImGui.Text("Name");

                ImGui.NextColumn();
                ImGui.PushItemWidth(ImGui.GetColumnWidth() - 12);
                ImGui.InputText($"##{name}", ref name, 512);

                ImGui.Columns(1);

                if (ImGui.BeginChild("Placement"))
                {
                    PlacementNode(mSelectedActor);
                }

                /* actor parameters are loaded from the dynamic node */
                if (mSelectedActor.mActorParameters.Count > 0)
                {
                    DynamicParamNode(mSelectedActor);
                }
            }
            

            if (status)
            {
                ImGui.End();
            }
        }

        private void AreaParameterPanel()
        {
            bool status = ImGui.Begin("Course Area Parameters");

            ImGui.Text(selectedArea.GetName());

            AreaParameters(selectedArea.mAreaParams);

            if (status)
            {
                ImGui.End();
            }
        }

        private static void AreaParameters(CourseArea.AreaParam area)
        {
            ParamHolder areaParams = ParamLoader.GetHolder("AreaParam");

            foreach (string key in areaParams.Keys)
            {
                string paramType = areaParams[key];

                if (!area.ContainsParam(key))
                {
                    continue;
                }

                switch (paramType)
                {
                    case "String":
                        string value = (string)area.GetParam(area.GetRoot(), key, paramType);
                        ImGui.InputText(key, ref value, 1024);

                        break;
                }
            }
        }

        private void FillLayers(CourseActorHolder actorArray)
        {
            foreach (CourseActor actor in actorArray.GetActors())
            {
                string actorLayer = actor.mLayer;
                mLayersVisibility[actorLayer] = true;
            }

            mHasFilledLayers = true;
        }

        private void CourseActorsLayerView(CourseActorHolder actorArray)
        {
            if (!mHasFilledLayers)
            {
                FillLayers(actorArray);
            }

            if (ImGui.Checkbox("All Layers", ref mAllLayersVisible))
            {
                foreach (string layer in mLayersVisibility.Keys)
                {
                    mLayersVisibility[layer] = mAllLayersVisible;
                }
            }

            foreach (string layer in mLayersVisibility.Keys)
            {
                bool isVisible = mLayersVisibility[layer];
                if (ImGui.Checkbox("##" + layer, ref isVisible))
                {
                    mLayersVisibility[layer] = isVisible;
                }

                ImGui.SameLine();

                if (!isVisible)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.CollapsingHeader(layer, ImGuiTreeNodeFlags.Selected))
                {
                    ImGui.Indent();
                    ImGui.PushItemWidth(ImGui.GetColumnWidth());
                    if (ImGui.BeginListBox("##" + layer))
                    {
                        foreach (CourseActor actor in actorArray.GetActors())
                        {
                            string actorName = actor.mActorName;
                            string name = actor.mName;
                            ulong actorHash = actor.mActorHash;
                            string actorLayer = actor.mLayer;

                            if (actorLayer != layer)
                            {
                                continue;
                            }

                            bool isSelected = (actor == mSelectedActor);

                            ImGui.PushID(actorHash.ToString());
                            ImGui.Columns(2);
                            if (ImGui.Selectable(actorName, isSelected, ImGuiSelectableFlags.SpanAllColumns))
                            {
                                mSelectedActor = actor;
                            }
                            ImGui.NextColumn();
                            ImGui.BeginDisabled();
                            ImGui.Text(name);
                            ImGui.EndDisabled();
                            ImGui.Columns(1);

                            ImGui.PopID();
                        }
                        ImGui.EndListBox();
                    }
                    ImGui.Unindent();
                }

                if (!isVisible)
                {
                    ImGui.EndDisabled();
                }
            }
        }

        private static void PlacementNode(CourseActor actor)
        {
            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                ImGui.Columns(2);

                ImGui.Text("Scale");
                ImGui.NextColumn();

                ImGui.PushItemWidth(ImGui.GetColumnWidth() - 12);

                if (ImGui.DragFloat3($"##Scale", ref actor.mScale))
                {

                }
                ImGui.PopItemWidth();

                ImGui.NextColumn();

                ImGui.Columns(1);
                ImGui.Unindent();

                ImGui.Indent();
                ImGui.Columns(2);

                ImGui.Text("Rotation");
                ImGui.NextColumn();

                ImGui.PushItemWidth(ImGui.GetColumnWidth() - 12);

                if (ImGui.DragFloat3($"##Rotation", ref actor.mRotation))
                {

                }
                ImGui.PopItemWidth();

                ImGui.NextColumn();

                ImGui.Columns(1);
                ImGui.Unindent();

                ImGui.Indent();
                ImGui.Columns(2);

                ImGui.Text("Translate");
                ImGui.NextColumn();

                ImGui.PushItemWidth(ImGui.GetColumnWidth() - 12);

                if (ImGui.DragFloat3($"##Translate", ref actor.mTranslation))
                {

                }
                ImGui.PopItemWidth();

                ImGui.NextColumn();

                ImGui.Columns(1);
                ImGui.Unindent();
            }
        }

        private void DynamicParamNode(CourseActor actor)
        {
            List<string> actorParams = ParamDB.GetActorComponents(actor.mActorName);

            foreach (string param in actorParams)
            {
                Dictionary<string, ParamDB.ComponentParam> dict = ParamDB.GetComponentParams(param);

                if (dict.Keys.Count == 0)
                {
                    continue;
                }

                if (ImGui.CollapsingHeader(param))
                {
                    ImGui.Indent();

                    ImGui.Columns(2);

                    foreach (KeyValuePair<string, ParamDB.ComponentParam> pair in ParamDB.GetComponentParams(param))
                    {
                        string id = $"##{pair.Key}";

                        ImGui.Text(pair.Key);
                        ImGui.NextColumn();

                        ImGui.PushItemWidth(ImGui.GetColumnWidth() - 12);

                        if (actor.mActorParameters.ContainsKey(pair.Key))
                        {
                            var actorParam = actor.mActorParameters[pair.Key];

                            switch (pair.Value.Type)
                            {
                                case "S16":
                                case "S32":
                                    int val_int = (int)actorParam;
                                    if (ImGui.InputInt(id, ref val_int))
                                    {
                                        actor.mActorParameters[pair.Key] = val_int;
                                    }
                                    break;
                                case "Bool":
                                    bool val_bool = (bool)actorParam;
                                    if (ImGui.Checkbox(id, ref val_bool))
                                    {
                                        actor.mActorParameters[pair.Key] = val_bool;
                                    }
                                    break;
                                case "F32":
                                    float val_float = (float)actorParam;
                                    if (ImGui.InputFloat(id, ref val_float)) 
                                    {
                                        actor.mActorParameters[pair.Key] = val_float;
                                    }
                                    break;
                                case "String":
                                    string val_string = (string)actorParam;
                                    if (ImGui.InputText(id, ref val_string, 1024))
                                    {
                                        actor.mActorParameters[pair.Key] = val_string;
                                    }
                                    break;
                                case "F64":
                                    double val = (double)actorParam;
                                    if (ImGui.InputDouble(id, ref val))
                                    {
                                        actor.mActorParameters[pair.Key] = val;
                                    }
                                    break;
                            }
                        }

                        ImGui.PopItemWidth();

                        ImGui.NextColumn();
                    }

                    ImGui.Columns(1);

                    ImGui.Unindent();
                }
            }
        }

        public Course GetCourse()
        {
            return course;
        }
    }
}
