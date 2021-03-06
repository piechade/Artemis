﻿using System;
using System.Linq;
using System.Windows.Forms;
using SharpVk;
using SharpVk.Khronos;

namespace Artemis.UI.SkiaSharp.Vulkan
{
    internal sealed class Win32VkContext : VkContext
    {
        public NativeWindow Window { get; }

        public Win32VkContext()
        {
            Window = new NativeWindow();
            Instance = Instance.Create(null, new[] {"VK_KHR_surface", "VK_KHR_win32_surface"});
            PhysicalDevice = Instance.EnumeratePhysicalDevices().First();
            Surface = Instance.CreateWin32Surface(Kernel32.CurrentModuleHandle, Window.Handle);

            (GraphicsFamily, PresentFamily) = FindQueueFamilies();

            DeviceQueueCreateInfo[] queueInfos =
            {
                new() {QueueFamilyIndex = GraphicsFamily, QueuePriorities = new[] {1f}},
                new() {QueueFamilyIndex = PresentFamily, QueuePriorities = new[] {1f}}
            };
            Device = PhysicalDevice.CreateDevice(queueInfos, null, null);
            GraphicsQueue = Device.GetQueue(GraphicsFamily, 0);
            PresentQueue = Device.GetQueue(PresentFamily, 0);

            GetProc = (name, instanceHandle, deviceHandle) =>
            {
                if (deviceHandle != IntPtr.Zero)
                    return Device.GetProcedureAddress(name);

                return Instance.GetProcedureAddress(name);
            };

            SharpVkGetProc = (name, instance, device) =>
            {
                if (device != null)
                    return device.GetProcedureAddress(name);
                if (instance != null)
                    return instance.GetProcedureAddress(name);

                // SharpVk includes the static functions on Instance, but this is not actually correct
                // since the functions are static, they are not tied to an instance. For example,
                // VkCreateInstance is not found on an instance, it is creating said instance.
                // Other libraries, such as VulkanCore, use another type to do this.
                return Instance.GetProcedureAddress(name);
            };
        }

        public override void Dispose()
        {
            base.Dispose();
            Window.DestroyHandle();
        }

        private (uint, uint) FindQueueFamilies()
        {
            QueueFamilyProperties[] queueFamilyProperties = PhysicalDevice.GetQueueFamilyProperties();

            var graphicsFamily = queueFamilyProperties
                .Select((properties, index) => new {properties, index})
                .SkipWhile(pair => !pair.properties.QueueFlags.HasFlag(QueueFlags.Graphics))
                .FirstOrDefault();

            if (graphicsFamily == null)
                throw new Exception("Unable to find graphics queue");

            uint? presentFamily = default;

            for (uint i = 0; i < queueFamilyProperties.Length; ++i)
            {
                if (PhysicalDevice.GetSurfaceSupport(i, Surface))
                    presentFamily = i;
            }

            if (!presentFamily.HasValue)
                throw new Exception("Unable to find present queue");

            return ((uint) graphicsFamily.index, presentFamily.Value);
        }
    }
}