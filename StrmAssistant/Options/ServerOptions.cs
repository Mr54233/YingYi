using Emby.Web.GenericEdit;
using MediaBrowser.Model.Attributes;
using MediaBrowser.Model.LocalizationAttributes;
using StrmAssistant.Properties;
using System.ComponentModel;

namespace StrmAssistant.Options
{
    public class ServerOptions : EditableOptionsBase
    {
        [DisplayNameL("NetworkOptions_EditorTitle_Network", typeof(Resources))]
        public override string EditorTitle => Resources.NetworkOptions_EditorTitle_Network;

        public enum ProxyModeOption
        {
            [DescriptionL("ServerOptions_ProxyMode_Off_Off", typeof(Resources))]
            Off,
            [DescriptionL("ServerOptions_ProxyMode_Whitelist_Whitelist", typeof(Resources))]
            Whitelist,
            [DescriptionL("ServerOptions_ProxyMode_All_All", typeof(Resources))]
            All
        }

        [DisplayNameL("ServerOptions_ProxyMode_Proxy_Mode", typeof(Resources))]
        [DescriptionL("ServerOptions_ProxyMode_Which_outgoing_server_requests_go_through_the_proxy__Not_effective_in_this_build_yet__Default_is_Off_", typeof(Resources))]
        public ProxyModeOption ProxyMode { get; set; } = ProxyModeOption.Off;

        [DisplayNameL("ServerOptions_ProxyServer_Proxy_Server", typeof(Resources))]
        [DescriptionL("ServerOptions_ProxyServer_Proxy_host_and_port_like_127_0_0_1_7890__Not_effective_in_this_build_yet__Default_is_BLANK_", typeof(Resources))]
        public string ProxyServer { get; set; } = string.Empty;

        [DisplayNameL("ServerOptions_ProxyBypassList_Proxy_Bypass_List", typeof(Resources))]
        [DescriptionL("ServerOptions_ProxyBypassList_Hosts_that_bypass_the_proxy__Comma_separated__Not_effective_in_this_build_yet__Default_is_BLANK_", typeof(Resources))]
        public string ProxyBypassList { get; set; } = string.Empty;

        [DisplayNameL("ServerOptions_LocalDiscoveryUrl_Local_Discovery_Url", typeof(Resources))]
        [DescriptionL("ServerOptions_LocalDiscoveryUrl_Custom_url_for_local_network_discovery__Reserved_for_a_future_build__Default_is_BLANK_", typeof(Resources))]
        public string LocalDiscoveryUrl { get; set; } = string.Empty;

        [DisplayNameL("ServerOptions_MemoryUsageLimitMb_Memory_Usage_Limit_MB", typeof(Resources))]
        [DescriptionL("ServerOptions_MemoryUsageLimitMb_Cap_server_memory_usage_in_MB__0_means_no_limit__Reserved_for_a_future_build__Default_is_0_", typeof(Resources))]
        public int MemoryUsageLimitMb { get; set; } = 0;

        [DisplayNameL("ServerOptions_ReverseLogLines_Reverse_Log_Lines", typeof(Resources))]
        [DescriptionL("ServerOptions_ReverseLogLines_Show_newest_log_lines_first_in_the_web_dashboard__Reserved_for_a_future_build__Default_is_OFF_", typeof(Resources))]
        [Required]
        public bool ReverseLogLines { get; set; } = false;
    }
}
