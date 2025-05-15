using EightSixteenEmu;
using EightSixteenEmu.Devices;
using EightSixteenEmu.MemoryMapping;
using System.Text.Json.Nodes;


namespace EmuXTesting
{
    public class MapperTests
    {
        [Fact]
        public void AddDevice_ShouldMapCorrectly()
        {
            EmuCore emu = new EmuCore();
            var device = new DevRAM(0x1000);
            emu.Mapper.AddDevice(device, 0x0000, 0x0000, 0x1000);

            // Check if the device is mapped correctly
            Assert.NotNull(emu.Mapper[0x0000]);
            Assert.NotNull(emu.Mapper[0x0FFF]);
            Assert.Null(emu.Mapper[0x1000]); // end value should be exclusive
        }

        [Fact]
        public void AddDevice_ShouldNotAllowZeroLength()
        {
            EmuCore emu = new EmuCore();
            var device = new DevRAM(0x1000);
            Assert.Throws<ArgumentOutOfRangeException>(() => emu.Mapper.AddDevice(device, 0x0000, 0x0000, 0));
        }

        [Fact]
        public void ToJson_ShouldSerializeDevicesAndMappings()
        {
            // Arrange
            EmuCore emu = new EmuCore();
            var device = new DevRAM(0x1000);
            emu.Mapper.AddDevice(device, 0x2000, 0x0000, 0x1000);

            // Act
            JsonObject json = emu.Mapper.ToJson();

            // Assert
            Assert.NotNull(json);
            Assert.True(json.ContainsKey("devices"));
            Assert.True(json.ContainsKey("mappings"));

            var devices = json["devices"]!.AsArray();
            var mappings = json["mappings"]!.AsArray();

            Assert.Single(devices);
            Assert.Single(mappings);

            var devDef = devices[0]!.AsObject();
            Assert.Equal(device.guid.ToString(), devDef["guid"]!.GetValue<string>());
            Assert.Equal("EightSixteenEmu.Devices.DevRAM", devDef["type"]!.GetValue<string>());

            var mapDef = mappings[0]!.AsObject();
            Assert.Equal(0x2000u, mapDef["address"]!.GetValue<uint>());
            Assert.Equal(0x1000u, mapDef["length"]!.GetValue<uint>());
            Assert.Equal(device.guid.ToString(), mapDef["device"]!.GetValue<string>());
            Assert.Equal(0u, mapDef["offset"]!.GetValue<uint>());
        }

        [Fact]
        public void FromJson_ShouldRestoreDevicesAndMappings()
        {
            // Arrange: create a mapper, add a device, serialize to JSON
            EmuCore emu = new EmuCore();
            var device = new DevRAM(0x800);
            emu.Mapper.AddDevice(device, 0x4000, 0x0000, 0x800);
            JsonObject json = emu.Mapper.ToJson();

            // Act: create a new mapper and restore from JSON
            var newMapper = new MemoryMapper();
            newMapper.FromJson(json);

            // Assert: mapping should exist in the new mapper
            Assert.NotNull(newMapper[0x4000]);
            Assert.NotNull(newMapper[0x47FF]);
            Assert.Null(newMapper[0x4800]);

            // Devices should be restored with correct GUID
            JsonArray devices = json["devices"]!.AsArray();
            var guid = devices[0]!.AsObject()["guid"]!.GetValue<string>();
            var mappedDevice = newMapper.GetDeviceAt(0x4000);
            Assert.NotNull(mappedDevice);
            Assert.Equal(guid, mappedDevice!.guid.ToString());
        }

        [Fact]
        public void FromJson_ShouldThrowIfDevicesMissing()
        {
            var json = new JsonObject
            {
                ["mappings"] = new JsonArray()
            };
            var mapper = new MemoryMapper();
            Assert.Throws<ArgumentNullException>(() => mapper.FromJson(json));
        }

        [Fact]
        public void FromJson_ShouldThrowIfMappingsMissing()
        {
            var json = new JsonObject
            {
                ["devices"] = new JsonArray()
            };
            var mapper = new MemoryMapper();
            Assert.Throws<ArgumentNullException>(() => mapper.FromJson(json));
        }
    }
}
