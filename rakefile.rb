require 'albacore'
include REXML
include Rake::DSL
	
BUILD_NUMBER_BASE = "0.3.0"
PROJECT_NAME = "Bootstrap"
SLN_PATH = "source/#{PROJECT_NAME}.sln"
SLN_FILES = [SLN_PATH]

### Edit these settings 
DATABASE = "mobilecl125"
DATABASE_TYPE = "mssql"
DATABASE_CONNECTION = "Data Source=localhost;Initial Catalog=mobilecl125;User Id=sa;Password=sa"

COMPILE_TARGET = "Debug"

DOVETAILSDK_PATH = "#{Rake::Win32::normalize(ENV['ProgramW6432'].nil? ? ENV['PROGRAMFILES']: ENV['ProgramW6432'])}/Dovetail Software/fcSDK/bin".gsub('/','\\')
SCHEMAEDITOR_PATH = "#{Rake::Win32::normalize(ENV['PROGRAMFILES'])}/Dovetail Software/SchemaEditor/SchemaEditor.exe"

NUGET_EXE = File.absolute_path("nuget.exe")
NUGET_FEEDS = ["#{DOVETAILSDK_PATH}","https://go.microsoft.com/fwlink/?LinkID=230477"]

puts "Loading scripts from build support directory..."
buildsupportfiles = Dir["#{File.dirname(__FILE__)}/buildsupport/*.rb"]
buildsupportfiles.each { |ext| 
	puts "loading #{ext}" 
	load ext 
}

props = {:archive => "build", :testing => "results", :database => ""}

desc "**Default**, compiles and runs unit tests"
task :default => [:clean,:version,:compile,:test_assemblies,:unit_tests]

desc "Run unit and integration tests. **Requires Database**"
task :ci => [:default,:integration_tests]

desc "Build release version of web site"
task :build_release do 
	Rake::Task["compile"].execute(:target => :RELEASE)

	#releaseDir = "#{props[:testing]}/release"
	#puts "\nBuilt and copied release to: \n\n #{File.absolute_path(releaseDir)}\n"
	#FileUtils::cp_r 'source/Web/.', releaseDir 
	#FileUtils::rm_rf "#{releaseDir}/obj" 
end

desc "build solution"
task :compile => [:version, "nuget:install"] do |t, args|
	target = args[:target] || :DEBUG
 
	puts "Doing #{target} build" 

	SLN_FILES.each do |f|
		msb = MSBuild.new
		msb.properties :configuration => target
		msb.targets :Clean, :Build
		msb.verbosity = "minimal"
		msb.solution = f
		msb.execute
	end
end

#desc "Copy archives to test folder in order to run unit tests"
output :test_assemblies => [:compile] do |out|
	out.from "#{File.dirname(__FILE__)}"
	out.to "#{props[:testing]}"
	Dir.glob("**/bin/Debug*/*.*"){ |file|
		out.file file, :as => "assemblies/#{File.basename(file)}"
	}	
end

desc "Run unit tests for any dlls that end with 'tests'"
nunit :unit_tests do |nunit|	
	nunit.command = findNunitConsoleExe()
	nunit.assemblies = Dir.glob("results/assemblies/*{T,t}ests.dll").uniq
	nunit.options '/xml=results/unit-test-results.xml'
end

desc "Run integration tests for any dlls that end with 'tests'"
nunit :integration_tests do |nunit|	

	#update test assembly config files to have database connection details.
	Dir.glob("results/assemblies/*{I,i}ntegration.dll.config") { |appConfig|
		File.open(appConfig) { |c|
			doc = REXML::Document.new(c)
			doc.root.elements["/configuration/appSettings/add[@key='DovetailDatabase.Type']"].attributes['value'] = DATABASE_TYPE
			doc.root.elements["/configuration/appSettings/add[@key='DovetailDatabase.ConnectionString']"].attributes['value'] = DATABASE_CONNECTION
			formatter = REXML::Formatters::Default.new
			File.open(c, 'w') do |result|
				formatter.write(doc, result)
			end
		}
	}

	nunit.command = findNunitConsoleExe()
	nunit.assemblies = Dir.glob("results/assemblies/*{I,i}ntegration.dll").uniq
	nunit.options '/xml=results/integration-test-results.xml'
end

def findNunitConsoleExe
	nunitPackageDirectory = Dir.glob('source/packages/NUnit*').first
	raise "NUnit package was not found under source/packages." if nunitPackageDirectory.nil?
	
	return File.join(nunitPackageDirectory, 'tools/nunit-console.exe')
end

namespace :nuget do

	desc "Run nuget update on all the projects"
	task :update => [:clean] do 
		Dir.glob(File.join("**","packages.config")){ |file|
			puts "Updating packages for #{file}"
			sh "#{NUGET_EXE} update #{file} -RepositoryPath source/packages"
		}
	end
			
	desc "Run nuget install on all projects"
	task :install => [:clean] do 
		Dir.glob(File.join("**","packages.config")){ |file|
			packagesDir = File.absolute_path("source/packages").gsub('/','\\')
			packagesConfig = File.absolute_path(file)
			puts "Updating packages for #{packagesConfig}"
			sh "#{NUGET_EXE} install #{packagesConfig} -OutputDirectory #{packagesDir} -Source \"#{NUGET_FEEDS.join(";")}\""
		}
	end

	desc "Build nuget packages"
	task :build => [:build_release] do 
		FileUtils.mkdir_p("results/packages")
		packagesDir = File.absolute_path("results/packages")
		Dir.glob(File.join("**","*.nuspec")){ |file|
			puts "Building nuget package for #{file}"
			projectPath = File.dirname(file)
			Dir.chdir(projectPath) do 
				puts "in project path #{projectPath}"
				sh "#{NUGET_EXE} pack -OutputDirectory #{packagesDir} -Prop Configuration=Release -Symbols"
			end		
		}
	end

	desc "Deploy nuget packages. Expectes you to define your own 'deploy_nuget_packages' task in the buildsupport directory"
	task :deploy => [:default,"nuget:build",:deploy_nuget_packages]
end 

namespace :setup do 

	#desc "Rebuilds development database and populates it with data"
	task :developer => [:clean,:apply_schemascripts]
	
	desc "Apply all schema scripts in the schema directory"
	task :apply_schemascripts do
		sh "\"#{SCHEMAEDITOR_PATH}\" -g"
		apply_schema
	end

	def apply_schema(database = DATABASE)  

		puts "Applying scripts to #{database} database"
		seConfig = 'Default.SchemaEditor'           
		seReport = 'SchemaDifferenceReport.txt'

		#SchemaEditor has different (more verbose) database type configuration than Dovetail SDK
		databaseType = (DATABASE_TYPE == 'mssql') ? 'MsSqlServer2005' : 'Oracle9'

		Dir.glob(File.join('schema', "*schemascript.xml")) do |schema_script|  
 
			File.open(seConfig) do |schema_editor_config_file|
				doc = REXML::Document.new(schema_editor_config_file)
				doc.root.elements['database/type'].text = databaseType
				doc.root.elements['database/connectionString'].text = DATABASE_CONNECTION
				doc.root.elements['inputFilePath'].text = schema_script.gsub('/','\\')
				formatter = REXML::Formatters::Default.new
				File.open(seConfig, 'w') do |result|
					formatter.write(doc, result)
				end
			end

			puts "\n\nApplying schemascript #{schema_script}"
			sh "\"#{SCHEMAEDITOR_PATH}\" -a"
		end
		sh "type #{seReport}"
		File.delete(seConfig)
		File.delete seReport if File.exists? seReport
	end
end

#desc "Prepares the working directory for a new build"
task :clean do	

	props.each do |key,val|
		FileUtils.rm_r(Dir.glob("#{val}/*"), :force => true) if File.exists?val
		FileUtils.rmdir(val) if File.exists?val  
	end
	# Clean up all bin folders in the source folder
	FileUtils.rm_rf(Dir.glob("**/{obj,bin}"))

end

#desc "Update the version information for the build"
assemblyinfo :version do |asm|
	asm_version = BUILD_NUMBER_BASE + ".0"

	begin
		gittag = `git describe --long`.chomp 	# looks something like v0.1.0-63-g92228f4
		gitnumberpart = /-(\d+)-/.match(gittag)
		gitnumber = gitnumberpart.nil? ? '0' : gitnumberpart[1]
		commit = `git log -1 --pretty=format:%H`
	rescue
		commit = "git unavailable"
		gitnumber = "0"
	end
	build_number = "#{BUILD_NUMBER_BASE}.#{gitnumber}"
	puts "Git based version is #{build_number}"
	asm.trademark = commit
	asm.version = build_number
	asm.file_version = build_number
	asm.custom_attributes :AssemblyInformationalVersion => build_number
	asm.output_file = 'source/CommonAssemblyInfo.cs'
end

desc "Build and host the web application in iisexpress on port 7070"
task :start_web => [:compile] do
	puts "\n\n\n*** Launching iis express for this web application on port 7070 ***\n\n\n"

	IISEXPRESS_EXE = "#{ENV['PROGRAMFILES']}/IIS Express/iisexpress.exe".gsub('/','\\')
	sh "\"#{IISEXPRESS_EXE}\" /path:#{File.absolute_path("source/Web").gsub('/','\\')} /port:7070 /clr:v4.0"
end