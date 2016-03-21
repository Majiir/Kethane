KSPDIR		:= ${HOME}/ksp/KSP_linux
MANAGED		:= ${KSPDIR}/KSP_Data/Managed
GAMEDATA	:= ${KSPDIR}/GameData
KGAMEDATA  := ${GAMEDATA}/Kethane
PLUGINDIR	:= ${KGAMEDATA}/Plugins

TARGETS		:= bin/Kethane.dll

K_FILES := \
	BodyResourceData.cs	\
	Generators/CellularResourceGenerator.cs	\
	Generators/LegacyResourceGenerator.cs	\
	Generators/RandomExtensions.cs	\
	IResourceGenerator.cs	\
	KethaneController.cs	\
	KethaneData.cs	\
	Misc.cs	\
	PartModules/HeatSinkAnimator.cs	\
	PartModules/IDetectorAnimator.cs	\
	PartModules/IExtractorAnimator.cs	\
	PartModules/KethaneConverter.cs	\
	PartModules/KethaneDetector.cs	\
	PartModules/KethaneDetectorAnimator.cs	\
	PartModules/KethaneDetectorAnimatorUnity.cs	\
	PartModules/KethaneDrillAnimator.cs	\
	PartModules/KethaneDrillAnimatorLegacy.cs	\
	PartModules/KethaneExtractor.cs	\
	PartModules/KethaneGenerator.cs	\
	PartModules/KethaneKerbalBlender.cs	\
	PartModules/KethaneParticleDynamics.cs	\
	PartModules/KethaneParticleEmitter.cs	\
	PartModules/KethaneWetMassIndicator.cs	\
	PartModules/OrthogonalIntake.cs	\
	PartModules/PartExtensions.cs	\
	PartModules/TimedMovingAverage.cs	\
	Properties/AssemblyInfo.cs	\
	ResourceData.cs	\
	ResourceDefinition.cs	\
	Scenarios/KethaneScanningTutorial.cs	\
	SettingsManager.cs	\
	UserInterface/MainMenuOverlay.cs	\
	UserInterface/MapOverlay.cs	\
	UserInterface/OverlayRenderer.cs	\
	UserInterface/TerrainData.cs	\
	Utilities/CompatibilityChecker.cs	\
	Utilities/InstallChecker.cs	\
	Utilities/InstallCleanup.cs	\
	Utilities/LicenseSentinel.cs	\
	Utilities/TutorialInstaller.cs	\
	$e

RESGEN2		:= resgen2
GMCS		:= mcs
GMCSFLAGS	:= -optimize -debug
GIT			:= git
TAR			:= tar
ZIP			:= zip

#all: version ${TARGETS}
all: ${TARGETS}

.PHONY: version
version:
	@./tools/git-version.sh

info:
	@echo "Kethane Build Information"
	@echo "    resgen2:    ${RESGEN2}"
	@echo "    gmcs:       ${GMCS}"
	@echo "    gmcs flags: ${GMCSFLAGS}"
	@echo "    git:        ${GIT}"
	@echo "    tar:        ${TAR}"
	@echo "    zip:        ${ZIP}"
	@echo "    KSP Data:   ${KSPDIR}"

bin/Kethane.dll: ${K_FILES} Kethane-LICENSE.txt Resources/AlphaUnlitVertexColored.shader
	@mkdir -p bin
	${GMCS} ${GMCSFLAGS} -t:library -lib:${MANAGED} \
		-r:Assembly-CSharp,Assembly-CSharp-firstpass \
		-r:UnityEngine,UnityEngine.UI \
		-r:KSPUtil \
		-lib:../../../GeodesicGrid/bin \
		-r:GeodesicGrid \
		-resource:Kethane-LICENSE.txt,Kethane.Kethane-LICENSE.txt \
		-resource:Resources/AlphaUnlitVertexColored.shader,Kethane.Resources.AlphaUnlitVertexColored.txt \
		-out:$@ ${K_FILES}

clean:
	rm -f ${TARGETS} assembly/AssemblyInfo.cs bin/Kethane.version
	test -d bin && rmdir bin || true

install: all
	mkdir -p ${PLUGINDIR}
	cp ${TARGETS} ${PLUGINDIR}
#cp ${TARGETS} bin/Kethane.version ${PLUGINDIR}

.PHONY: all clean install
