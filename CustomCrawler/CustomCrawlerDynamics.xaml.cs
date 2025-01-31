﻿/***

   Copyright (C) 2020. rollrat. All Rights Reserved.
   
   Author: Custom Crawler Developer

***/

using CefSharp;
using CefSharp.Wpf;
using CustomCrawler.chrome_devtools;
using HtmlAgilityPack;
using MasterDevs.ChromeDevTools;
using MasterDevs.ChromeDevTools.Protocol.Chrome.DOM;
using MasterDevs.ChromeDevTools.Protocol.Chrome.Network;
using MasterDevs.ChromeDevTools.Protocol.Chrome.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CustomCrawler
{
    /// <summary>
    /// CustomCrawlerDynamics.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CustomCrawlerDynamics : Window
    {
        ChromiumWebBrowser browser;
        //CallbackCCW cbccw;
        CustomCrawlerDynamicsHover hover;
        public static bool opened = false;

        public CustomCrawlerDynamics()
        {
            InitializeComponent();

            browser = new ChromiumWebBrowser(string.Empty);
            browserContainer.Content = browser;

            browser.LoadingStateChanged += Browser_LoadingStateChanged;

            //CefSharpSettings.LegacyJavascriptBindingEnabled = true;
            //browser.JavascriptObjectRepository.Register("ccw", cbccw = new CallbackCCW(this), isAsync: true);

            Closed += CustomCrawlerDynamics_Closed;

            KeyDown += CustomCrawlerDynamics_KeyDown;

            if (opened)
            {
                Close();
                return;
            }

            opened = true;

            hover = new CustomCrawlerDynamicsHover(this);
            hover.Show();
        }

        #region Navigate & New instance

        public static IChromeSession ss;
        bool init = false;
        CustomCrawlerDynamicsRequest child;
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!init)
            {
                var token = new Random().Next();
                browser.LoadHtml(token.ToString());
                init = true;

                JsManager.Instance.Clear();
                requests = new List<RequestWillBeSentEvent>();
                requests_id = new Dictionary<string, RequestWillBeSentEvent>();
                what_is_near = new Dictionary<string, HashSet<int>>();
                response = new Dictionary<string, ResponseReceivedEvent>();
                scripts = new List<MasterDevs.ChromeDevTools.Protocol.Chrome.Debugger.ScriptParsedEvent>();
                ss = await ChromeDevTools.Create();
                (child = new CustomCrawlerDynamicsRequest(ss, this)).Show();
                init_overlay();

                _ = Application.Current.Dispatcher.BeginInvoke(new Action(
                delegate
                {
                    Navigate.IsEnabled = false;
                }));
            }

            browser.Load(URLText.Text);
        }

        #endregion

        #region Control Events

        private void CustomCrawlerDynamics_Closed(object sender, EventArgs e)
        {
            opened = false;
            ChromeDevTools.Dispose();
            if (child != null)
                child.Close();
            childs.Where(x => x.IsLoaded).ToList().ForEach(x => x.Close());
            hover.Close();
        }

        bool locking;
        private async void CustomCrawlerDynamics_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)
            {
                locking = !locking;
                if (locking)
                    await enter_overlay();
                else
                    await leave_overlay();
            }
            else if (e.Key == Key.F5)
            {
                ss.SendAsync<MasterDevs.ChromeDevTools.Protocol.Chrome.Page.ReloadCommand>().Wait();
            }
        }

        private void Browser_LoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            if (!e.IsLoading && ss != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(
                delegate
                {
                    Build.IsEnabled = true;
                }));
            }
        }

        #endregion

        #region Cef Callback

        public static bool ignore_js(string url)
        {
            var filename = url.Split('?')[0].Split('/').Last();
            
            if (filename.StartsWith("jquery"))
                return true;

            //switch (filename.ToLower())
            //{
            //    case "js.cookie.js":
            //    case "html5shiv.min.js":
            //    case "yall.js":
            //    case "moment.js":
            //    case "yall.min.js":
            //    case "underscore.js":
            //    case "partial.js":
            //        return true;
            //}

            return false;
        }

        //bool locking = false;
        //public class CallbackCCW
        //{
        //    CustomCrawlerDynamics instance;
        //    string before = "";
        //    public string before_border = "";
        //    public CallbackCCW(CustomCrawlerDynamics instance)
        //    {
        //        this.instance = instance;
        //    }
        //    public void hoverelem(string elem)
        //    {
        //        if (instance.locking) return;
        //        Application.Current.Dispatcher.BeginInvoke(new Action(
        //        async delegate
        //        {
        //            instance.browser.GetMainFrame().EvaluateScriptAsync($"document.querySelector('[{before}]').style.border = '{before_border}';").Wait();
        //            before = $"ccw_tag={elem}";
        //            before_border = instance.browser.GetMainFrame().EvaluateScriptAsync($"document.querySelector('[{before}]').style.border").Result.Result.ToString();
        //            instance.browser.GetMainFrame().EvaluateScriptAsync($"document.querySelector('[{before}]').style.border = '0.2em solid red';").Wait();
        //
        //            await instance.hover.Update(elem);
        //        }));
        //    }
        //}

        List<Window> childs = new List<Window>();
        private void Hyperlink_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hyperlink = (Hyperlink)sender;
            if (hyperlink.DataContext == null)
                System.Diagnostics.Process.Start(hyperlink.NavigateUri.ToString());
            else
            {
                var child = new CustomCrawlerDynamicsRequestInfo(requests[(hyperlink.DataContext as int?).Value], response[requests[(hyperlink.DataContext as int?).Value].RequestId]);
                child.Show();
                childs.Add(child);
            }
        }

        #endregion

        #region Build for Hover

        public Dictionary<string, StackTrace> stacks = new Dictionary<string, StackTrace>();
        //private async Task find_source(Node nn)
        //{
        //    _ = Application.Current.Dispatcher.BeginInvoke(new Action(
        //    delegate
        //    {
        //        URLText.Text = nn.NodeId.ToString();
        //    }));
        //    var st = await ss.SendAsync(new GetNodeStackTracesCommand { NodeId = nn.NodeId });
        //    if (st.Result != null && st.Result.Creation != null)
        //    {
        //        stacks.Add("ccw_" + nn.NodeId, st.Result.Creation);
        //    }
        //    if (nn.NodeType == 1)
        //    {
        //        await ss.SendAsync(new SetAttributeValueCommand
        //        {
        //            NodeId = nn.NodeId,
        //            Name = "ccw_tag",
        //            Value = "ccw_" + nn.NodeId
        //        });
        //        await ss.SendAsync(new SetAttributeValueCommand
        //        {
        //            NodeId = nn.NodeId,
        //            Name = "onmouseenter",
        //            Value = $"ccw.hoverelem('ccw_{nn.NodeId}')"
        //        });
        //        await ss.SendAsync(new SetAttributeValueCommand
        //        {
        //            NodeId = nn.NodeId,
        //            Name = "onmouseleave",
        //            Value = $"ccw.hoverelem('ccw_{nn.NodeId}')"
        //        });
        //    }
        //    if (nn.Children != null)
        //    {
        //        foreach (var child in nn.Children)
        //        {
        //            await find_source(child);
        //        }
        //    }
        //}

        private void init_overlay()
        {
            ss.Subscribe<MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.InspectNodeRequestedEvent>(async x =>
            {
                await Application.Current.Dispatcher.BeginInvoke(new Action(
                async delegate
                {
                    await hover.Update(x.BackendNodeId);
                }));
            });

            ss.Subscribe<MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.NodeHighlightRequestedEvent>(x =>
            {
                ;
            });

            //ss.Subscribe<MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.InspectModeCanceledEvent>(x =>
            //{
            //    ;
            //});
        }

        private Task enter_overlay()
        {
            return ss.SendAsync(new MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.SetInspectModeCommand
            {
                Mode = "searchForNode",
                HighlightConfig = new MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.HighlightConfig
                {
                    ShowInfo = true,
                    ShowRulers = false,
                    ShowStyles = true,
                    ShowExtensionLines = false,
                    ContentColor = new RGBA { R = 111, G = 168, B = 220, A = 0.66 },
                    PaddingColor = new RGBA { R = 147, G = 196, B = 125, A = 0.55 },
                    BorderColor = new RGBA { R = 255, G = 229, B = 153, A = 0.66 },
                    MarginColor = new RGBA { R = 246, G = 178, B = 107, A = 0.66 },
                    EventTargetColor = new RGBA { R = 255, G = 196, B = 196, A = 0.66 },
                    ShapeColor = new RGBA { R = 96, G = 82, B = 177, A = 0.8 },
                    ShapeMarginColor = new RGBA { R = 96, G = 82, B = 127, A = 0.6 },
                    CssGridColor = new RGBA { R = 75, G = 0, B = 130 },
                }
            });
        }

        private Task leave_overlay()
        {
            return ss.SendAsync(new MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.SetInspectModeCommand
            {
                // searchForNode, searchForUAShadowDOM, captureAreaScreenshot, showDistances, none
                Mode = "none",
                HighlightConfig = new MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.HighlightConfig
                {
                    ShowInfo = true,
                    ShowRulers = false,
                    ShowStyles = true,
                    ShowExtensionLines = false,
                    ContentColor = new RGBA { R = 111, G = 168, B = 220, A = 0.66 },
                    PaddingColor = new RGBA { R = 147, G = 196, B = 125, A = 0.55 },
                    BorderColor = new RGBA { R = 255, G = 229, B = 153, A = 0.66 },
                    MarginColor = new RGBA { R = 246, G = 178, B = 107, A = 0.66 },
                    EventTargetColor = new RGBA { R = 255, G = 196, B = 196, A = 0.66 },
                    ShapeColor = new RGBA { R = 96, G = 82, B = 177, A = 0.8 },
                    ShapeMarginColor = new RGBA { R = 96, G = 82, B = 127, A = 0.6 },
                    CssGridColor = new RGBA { R = 75, G = 0, B = 130 },
                }
            });
            //await ss.SendAsync(new MasterDevs.ChromeDevTools.Protocol.Chrome.Overlay.HideHighlightCommand());
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            await Application.Current.Dispatcher.BeginInvoke(new Action(
            delegate
            {
                Build.IsEnabled = false;
            }));

            var doc = await ss.SendAsync(new GetDocumentCommand { Depth = -1 });

            if (doc.Result != null)
            {
                //stacks = new Dictionary<string, StackTrace>();
                //await find_source(doc.Result.Root);

            }
            else
            {
                await Application.Current.Dispatcher.BeginInvoke(new Action(
                delegate
                {
                    MessageBox.Show("Please try again later! " +
                        "If this error persists, restart the program. " +
                        "If that continues, please open new issue to https://github.com/rollrat/custom-crawler.", 
                        "Custom Craweler", MessageBoxButton.OK, MessageBoxImage.Error);
                }));
            }

            await Application.Current.Dispatcher.BeginInvoke(new Action(
            delegate
            {
                Build.IsEnabled = true;
            }));
        }

        #endregion

        // <js filename, requests>
        public List<RequestWillBeSentEvent> requests;
        public Dictionary<string, RequestWillBeSentEvent> requests_id;
        Dictionary<string, HashSet<int>> what_is_near;
        public void add_request_info(RequestWillBeSentEvent request)
        {
            if (!requests_id.ContainsKey(request.RequestId))
                requests_id.Add(request.RequestId, request);

            // Metadata files are filtered here.
            if (request.Initiator == null || request.Initiator.Stack == null ||
                request.Initiator.Stack.CallFrames == null || request.Initiator.Stack.CallFrames.Length == 0)
                return;

            if (request.Request.Url == "")
                return;

            if (ignore_js(request.Request.Url))
                return;

            lock (requests)
                requests.Add(request);
            var index = requests.Count - 1;

            var stack = request.Initiator.Stack;

            lock (what_is_near)
            {
                while (stack != null)
                {
                    foreach (var frame in stack.CallFrames)
                    {
                        if (!what_is_near.ContainsKey(frame.Url))
                            what_is_near.Add(frame.Url, new HashSet<int>());
                        what_is_near[frame.Url].Add(index);
                    }
                    stack = stack.Parent;
                }
            }
        }

        public Dictionary<string, ResponseReceivedEvent> response;
        public void add_response_info(ResponseReceivedEvent res)
        {
            lock (response)
                if (!response.ContainsKey(res.RequestId))
                    response.Add(res.RequestId, res);
        }

        public List<MasterDevs.ChromeDevTools.Protocol.Chrome.Debugger.ScriptParsedEvent> scripts;
        public void add_script_info(MasterDevs.ChromeDevTools.Protocol.Chrome.Debugger.ScriptParsedEvent spe)
        {
            if (spe.Url == "")
            {
                var x = ss.SendAsync(new MasterDevs.ChromeDevTools.Protocol.Chrome.Debugger.GetScriptSourceCommand
                {
                    ScriptId = spe.ScriptId
                }).Result;

                ;
            }
            scripts.Add(spe);
        }

        public List<(CallFrame, int, int, int)> pick_candidate(string url, List<Esprima.Ast.INode> node, string function_name, int line, int column)
        {
            var pre = new List<(CallFrame, int)>();
            HashSet<int> ll;

            lock (what_is_near)
            {
                if (!what_is_near.ContainsKey(url))
                    return new List<(CallFrame, int, int, int)>();
                ll = what_is_near[url];
            }

            var cnt = ll.Count;
            for (int i = 0; i < cnt; i++)
            {
                var item = requests[ll.ElementAt(i)];
                var stack = item.Initiator.Stack;

                while (stack != null)
                {
                    foreach (var frame in stack.CallFrames)
                    {
                        if (frame.Url != url)
                            continue;
                        pre.Add((frame, ll.ElementAt(i)));
                    }

                    stack = stack.Parent;
                }
            }

            // (frame, requst_index, valid_node_index, distance)
            var result = new List<(CallFrame, int, int, int)>();

            foreach (var frame in pre)
            {
                if (frame.Item1.FunctionName == function_name && frame.Item1.LineNumber == line && frame.Item1.ColumnNumber == column)
                {
                    result.Add((frame.Item1, frame.Item2, -1, 0));
                    continue;
                }

                var route = JsManager.Instance.FindByLocation(frame.Item1.Url, (int)frame.Item1.LineNumber + 1, (int)frame.Item1.ColumnNumber + 1);
                var min = Math.Min(route.Count, node.Count);
                var precom = -1;

                for (int i = 0; i < min; i++)
                {
                    if (route[i].Type != node[i].Type)
                        break;
                    if (route[i].Location != node[i].Location)
                        break;

                    precom = i;
                }

                var validcom = precom;

                while (validcom >= 0)
                {
                    if (route[validcom].Type == Esprima.Ast.Nodes.CallExpression ||
                        route[validcom].Type == Esprima.Ast.Nodes.BlockStatement ||
                        route[validcom].Type == Esprima.Ast.Nodes.FunctionExpression)
                    {
                        break;
                    }

                    validcom--;
                }

                if (validcom >= 0)
                {
                    result.Add((frame.Item1, frame.Item2, validcom, Math.Abs(validcom - node.Count)));
                }
            }

            result.Sort((x, y) => y.Item4.CompareTo(x.Item4));

            return result;
        }

    }
}
